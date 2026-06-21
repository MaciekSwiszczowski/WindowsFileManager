using Windows.Storage;
using Windows.Storage.FileProperties;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.StorageItemDisposal;

/// <summary>
/// Head-to-head comparison of the two per-selection thumbnail paths the inspector can use: the current WinRT
/// <c>StorageFile.GetThumbnailAsync</c> path versus the candidate Win32 Shell path
/// (<c>SHCreateItemFromParsingName</c> → <c>IShellItemImageFactory::GetImage</c>) behind
/// <see cref="IShellThumbnailInterop"/>.
/// </summary>
/// <remarks>
/// <para>
/// Both arms start from a <em>path</em> and produce a usable thumbnail, so the WinRT arm deliberately includes the
/// <see cref="StorageFile"/> acquisition: the Win32 path eliminates that acquisition entirely, and it is a primary
/// source of the non-disposable runtime-callable wrappers (and their finalizer churn) this migration targets.
/// Measuring from the path is therefore the honest "what the inspector pays per selection" comparison.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> the <see cref="MemoryDiagnoserAttribute"/> <c>Allocated</c> column is the
/// key managed signal — the WinRT arm should allocate several RCWs per file (released only by the finalizer) while
/// the Win32 arm allocates one deterministically-released RCW plus the pixel buffer. The
/// <see cref="NativeMemoryProfiler"/> delta (run elevated per AGENTS.md §9) shows the native side. The WinRT arm is
/// the baseline so the Win32 arm reports as a ratio.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("Thumbnail", "StorageItem", "Win32", "WinRT")]
// ReSharper disable once ClassCanBeSealed.Global
public class ShellThumbnailComparisonBenchmarks
{
    // 48px matches the size the inspector thumbnail handler requests today.
    private const uint ThumbnailSize = 48;

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
    private string[] _paths = [];
    private IContainer? _container;
    private IShellThumbnailInterop? _shellThumbnails;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(ShellThumbnailComparisonBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _paths = CreateImageFiles("png", FileCount);
        _container = CreateContainer();
        _shellThumbnails = _container.Resolve<IShellThumbnailInterop>();
    }

    /// <summary>
    /// Current path: acquire a <see cref="StorageFile"/> from each path, retrieve and dispose its thumbnail, summing
    /// the reported sizes so the work is not optimized away.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task<ulong> WinRt_StorageFileThumbnail()
    {
        ulong total = 0;

        foreach (var path in _paths)
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await file
                .GetThumbnailAsync(ThumbnailMode.SingleItem, ThumbnailSize, ThumbnailOptions.ResizeThumbnail)
                .AsTask()
                .ConfigureAwait(false);
            if (thumbnail is not null)
            {
                total += thumbnail.Size;
            }
        }

        return total;
    }

    /// <summary>
    /// Candidate path: extract each thumbnail straight from the path via the Win32 Shell imaging COM API, summing
    /// the copied pixel-buffer lengths so the work is not optimized away.
    /// </summary>
    [Benchmark]
    public ulong Win32_ShellItemImageFactory()
    {
        var shellThumbnails = _shellThumbnails ?? throw new InvalidOperationException("Benchmark thumbnail interop is not initialized.");
        ulong total = 0;

        foreach (var path in _paths)
        {
            if (shellThumbnails.TryGetThumbnail(path, ThumbnailSize, out var thumbnail))
            {
                total += (ulong)thumbnail.Pixels.Length;
            }
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container?.Dispose();
        _container = null;
        _shellThumbnails = null;
        _paths = [];
        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private static IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.AddInfrastructureServices();

        return builder.Build();
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
