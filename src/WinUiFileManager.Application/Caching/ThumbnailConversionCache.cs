using System.Diagnostics.CodeAnalysis;

namespace WinUiFileManager.Application.Caching;

/// <summary>
/// A small bounded cache that skips re-converting identical thumbnails into the same image object. Holds the last
/// <see cref="DefaultCapacity"/> <i>used</i> images, keyed by a caller-supplied <see cref="ThumbnailContentHash"/>
/// that must fold in <b>every</b> input the conversion depends on (raw bytes <i>and</i> target dimensions, …) — not
/// just the bytes — so two thumbnails with byte-identical buffers but different dimensions do not collide. The value
/// is a caller-produced <typeparamref name="TImage"/> (e.g. a WinUI <c>SoftwareBitmapSource</c>).
/// </summary>
/// <remarks>
/// <para>
/// Implemented as a single ordered list (oldest → most-recently-used). Lookups/refreshes are linear scans, which is
/// deliberate: at this capacity a scan of ≤ 20 128-bit comparisons is cheaper and far simpler than the dictionary +
/// intrusive-linked-list machinery a large O(1) LRU would need. A hit moves its entry to the most-recently-used end;
/// inserting past capacity drops and disposes the oldest entry.
/// </para>
/// <para>
/// <b>Threading:</b> not thread-safe; intended for single-threaded use on the UI thread, where the image objects are
/// created and consumed. <b>Lifetime:</b> the cache owns every cached <typeparamref name="TImage"/> and disposes it
/// on eviction and on <see cref="Dispose"/>. The most-recently-used entry is never the eviction target, so with a
/// single on-screen consumer the currently displayed image is never disposed out from under it.
/// </para>
/// </remarks>
/// <typeparam name="TImage">The converted image type the cache owns and disposes.</typeparam>
public sealed class ThumbnailConversionCache<TImage> : IDisposable where TImage : class, IDisposable
{
    /// <summary>Default number of converted images to retain.</summary>
    private const int DefaultCapacity = 20;

    private readonly int _capacity;

    // Ordered oldest → newest; the last element is the most-recently-used. Bounded to _capacity.
    private readonly List<(ThumbnailContentHash Key, TImage Image)> _entries;
    private bool _disposed;

    /// <summary>Creates a cache retaining at most <paramref name="capacity"/> converted images.</summary>
    /// <param name="capacity">Maximum entries; must be at least 1.</param>
    public ThumbnailConversionCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _entries = new List<(ThumbnailContentHash, TImage)>(capacity);
    }

    /// <summary>Current number of cached images.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Returns the cached image for <paramref name="key"/> if present (marking it most-recently-used); otherwise
    /// invokes <paramref name="convert"/>, caches the result, and returns it — dropping and disposing the
    /// oldest image when capacity is exceeded.
    /// </summary>
    /// <param name="key">Fingerprint covering every input that determines the converted image (bytes, dimensions, …).</param>
    /// <param name="convert">Produces the image on a cache miss; must return a non-null value.</param>
    /// <returns>The cached or newly converted image, owned by the cache.</returns>
    /// <remarks>Single-threaded contract: must not be called after <see cref="Dispose"/> (trusted internal caller).</remarks>
    public TImage GetOrConvert(ThumbnailContentHash key, Func<TImage> convert)
    {
        if (TryTouch(key, out var cached))
        {
            return cached;
        }

        var image = convert() ?? throw new InvalidOperationException("Thumbnail conversion returned null.");
        Insert(key, image);
        return image;
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="GetOrConvert"/> for image types whose construction is asynchronous
    /// (e.g. <c>SoftwareBitmapSource.SetBitmapAsync</c>). Must be awaited on the UI thread.
    /// </summary>
    /// <param name="key">Fingerprint covering every input that determines the converted image (bytes, dimensions, …).</param>
    /// <param name="convertAsync">Produces the image on a cache miss; must resolve to a non-null value.</param>
    /// <returns>
    /// The cached or newly converted image (owned by the cache), or <see langword="null"/> if the cache was disposed
    /// while the conversion was in flight (e.g. the inspector closed mid-load).
    /// </returns>
    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP015:Member should not return created and cached instance",
        Justification = "This is a cache: returning a cache-owned image (cached or freshly converted) is the intended contract; the cache disposes it on eviction/disposal.")]
    public async Task<TImage?> GetOrConvertAsync(ThumbnailContentHash key, Func<Task<TImage>> convertAsync)
    {
        if (TryTouch(key, out var cached))
        {
            return cached;
        }

        var image = await convertAsync().ConfigureAwait(true) ?? throw new InvalidOperationException("Thumbnail conversion returned null.");

        // The await yields the UI thread, so another call may have cached the same content, or the cache may have
        // been disposed (the inspector closed mid-load). Disposal is a normal lifecycle event, not an error: orphan
        // the image we just built and return null rather than throwing — a thumbnail load must never crash the app.
        if (_disposed)
        {
            image.Dispose();
            return null;
        }

        if (TryTouch(key, out var raced))
        {
            image.Dispose();
            return raced;
        }

        Insert(key, image);
        return image;
    }

    private bool TryTouch(ThumbnailContentHash key, [NotNullWhen(true)] out TImage? image)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Key != key)
            {
                continue;
            }

            var entry = _entries[i];
            // Move to the most-recently-used end so a re-viewed thumbnail counts as "used" and survives eviction.
            _entries.RemoveAt(i);
            _entries.Add(entry);
            image = entry.Image;
            return true;
        }

        image = null;
        return false;
    }

    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP007:Don't dispose injected",
        Justification = "The cache takes ownership of images returned by the convert factory and is contractually responsible for disposing them on eviction.")]
    private void Insert(ThumbnailContentHash key, TImage image)
    {
        _entries.Add((key, image));

        // Adding one entry can exceed capacity by at most one, so dropping the single oldest restores the bound.
        if (_entries.Count <= _capacity)
        {
            return;
        }
        var oldest = _entries[0];
        _entries.RemoveAt(0);
        oldest.Image.Dispose();
    }

    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP007:Don't dispose injected",
        Justification = "The cache takes ownership of images returned by the convert factory and is contractually responsible for disposing them on disposal.")]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var entry in _entries)
        {
            entry.Image.Dispose();
        }

        _entries.Clear();
    }
}
