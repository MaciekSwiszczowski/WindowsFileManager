using System.Diagnostics.CodeAnalysis;

namespace WinUiFileManager.Application.Caching;

/// <summary>
/// A small bounded, content-addressed cache that skips re-converting identical thumbnail bytes into the same image
/// object. Keyed by <see cref="ThumbnailContentHash"/> of the raw bytes; the value is a caller-produced
/// <typeparamref name="TImage"/> (e.g. a WinUI <c>SoftwareBitmapSource</c>). Eviction is least-recently-used and the
/// evicted image is disposed.
/// </summary>
/// <remarks>
/// <para>
/// Intended use: the inspector always reads the thumbnail bytes (for correctness — custom/cloud thumbnails mean one
/// extension can map to many images), then calls <see cref="GetOrConvert"/> to avoid rebuilding the image when the
/// bytes are unchanged. This skips only the conversion step, never the byte read.
/// </para>
/// <para>
/// <b>Threading:</b> not thread-safe; intended for single-threaded use on the UI thread, where the image objects are
/// created and consumed. <b>Lifetime:</b> the cache owns every cached <typeparamref name="TImage"/> and disposes it
/// on eviction and on <see cref="Dispose"/>. The most-recently-returned image is at the head of the recency list and
/// is never the eviction target, so with a single on-screen consumer the currently displayed image is never disposed
/// out from under it.
/// </para>
/// </remarks>
/// <typeparam name="TImage">The converted image type the cache owns and disposes.</typeparam>
public sealed class ThumbnailConversionCache<TImage> : IDisposable
    where TImage : class, IDisposable
{
    /// <summary>Default number of converted images to retain.</summary>
    public const int DefaultCapacity = 20;

    private readonly int _capacity;
    private readonly Dictionary<ThumbnailContentHash, LinkedListNode<(ThumbnailContentHash Hash, TImage Image)>> _byHash;
    private readonly LinkedList<(ThumbnailContentHash Hash, TImage Image)> _recency;
    private bool _disposed;

    /// <summary>Creates a cache retaining at most <paramref name="capacity"/> converted images.</summary>
    /// <param name="capacity">Maximum entries; must be at least 1.</param>
    public ThumbnailConversionCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _byHash = new Dictionary<ThumbnailContentHash, LinkedListNode<(ThumbnailContentHash, TImage)>>(capacity);
        _recency = new LinkedList<(ThumbnailContentHash, TImage)>();
    }

    /// <summary>Current number of cached images.</summary>
    public int Count => _byHash.Count;

    /// <summary>
    /// Returns the cached image for <paramref name="content"/> if present (marking it most-recently-used); otherwise
    /// invokes <paramref name="convert"/>, caches the result, and returns it — evicting and disposing the
    /// least-recently-used image when capacity is exceeded.
    /// </summary>
    /// <param name="content">The raw thumbnail bytes whose fingerprint is the cache key.</param>
    /// <param name="convert">Produces the image on a cache miss; must return a non-null value.</param>
    /// <returns>The cached or newly converted image, owned by the cache.</returns>
    /// <exception cref="ObjectDisposedException">The cache has been disposed.</exception>
    public TImage GetOrConvert(ReadOnlySpan<byte> content, Func<TImage> convert)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = ThumbnailContentHash.Compute(content);
        if (_byHash.TryGetValue(key, out var hit))
        {
            _recency.Remove(hit);
            _recency.AddFirst(hit);
            return hit.Value.Image;
        }

        var image = convert() ?? throw new InvalidOperationException("Thumbnail conversion returned null.");
        var node = new LinkedListNode<(ThumbnailContentHash, TImage)>((key, image));
        _recency.AddFirst(node);
        _byHash.Add(key, node);

        // Adding one entry can exceed capacity by at most one, so a single eviction restores the bound.
        if (_byHash.Count > _capacity)
        {
            EvictLeastRecentlyUsed();
        }

        return image;
    }

    [SuppressMessage(
        "IDisposableAnalyzers.Correctness",
        "IDISP007:Don't dispose injected",
        Justification = "The cache takes ownership of images returned by the convert factory and is contractually responsible for disposing them on eviction.")]
    private void EvictLeastRecentlyUsed()
    {
        var lru = _recency.Last;
        if (lru is null)
        {
            return;
        }

        _recency.RemoveLast();
        _byHash.Remove(lru.Value.Hash);
        lru.Value.Image.Dispose();
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
        foreach (var entry in _recency)
        {
            entry.Image.Dispose();
        }

        _recency.Clear();
        _byHash.Clear();
    }
}
