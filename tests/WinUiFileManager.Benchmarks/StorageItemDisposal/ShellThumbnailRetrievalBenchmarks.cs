using Windows.Storage;
using Windows.Storage.FileProperties;

namespace WinUiFileManager.Benchmarks.StorageItemDisposal;

/// <summary>
/// Isolates the native cost of the Shell thumbnail pipeline (<c>StorageFile.GetThumbnailAsync</c>) from the cost of
/// acquiring the <see cref="StorageFile"/>. Addresses the concern that <c>ThumbnailHandlerBenchmarks</c> measures
/// acquisition and thumbnail extraction together and so cannot attribute native growth to either one.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="StorageFile"/> objects are acquired once in <see cref="Setup"/> and reused, so the per-iteration
/// measurement reflects only repeated thumbnail retrieval. Each returned <see cref="StorageItemThumbnail"/> is
/// disposed (it implements <see cref="IDisposable"/>), so any residual native growth is the Shell thumbnail cache —
/// a process-wide native cache that grows independently of managed object lifetime — rather than the thumbnail
/// stream handles.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> a flat <see cref="NativeMemoryProfiler"/> "native memory leak" delta means
/// the disposed thumbnails fully release their native memory and the app-level growth is acquisition-driven. A delta
/// that keeps climbing while the storage items are fixed indicts the Shell thumbnail cache and points the fix toward
/// a cache-trim or a Win32 <c>IThumbnailCache</c> path rather than more frequent GC.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("WinRT", "StorageItem")]
// ReSharper disable once ClassCanBeSealed.Global
public class ShellThumbnailRetrievalBenchmarks
{
    private const uint ThumbnailSize = 256;

    // A 1x1 PNG: enough for the Shell to produce a real thumbnail without embedding a large asset.
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    [Params(5, 20)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private StorageFile[] _files = [];

    [GlobalSetup]
    public async Task Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(ShellThumbnailRetrievalBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        var paths = CreateImageFiles("png", FileCount);
        _files = new StorageFile[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            _files[i] = await StorageFile.GetFileFromPathAsync(paths[i]);
        }
    }

    /// <summary>
    /// Retrieves and disposes a single-item thumbnail for each pre-acquired <see cref="StorageFile"/>, summing the
    /// reported sizes so the call is not optimized away. The storage items are fixed; only the thumbnail work varies.
    /// </summary>
    [Benchmark]
    public async Task<ulong> RetrieveThumbnails()
    {
        ulong total = 0;

        foreach (var file in _files)
        {
            using var thumbnail = await file
                .GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize)
                .AsTask()
                .ConfigureAwait(false);
            if (thumbnail is not null)
            {
                total += thumbnail.Size;
            }
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _files = [];
        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private string[] CreateImageFiles(string groupName, int count)
    {
        var directory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(directory);

        var paths = new string[count];
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(directory, $"file-{i:D6}.png");
            File.WriteAllBytes(path, MinimalPng);
            paths[i] = path;
        }

        return paths;
    }
}
