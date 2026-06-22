using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.Interop;

/// <summary>
/// Measures the new Win32 Shell thumbnail path (<see cref="IShellThumbnailInterop.TryGetThumbnail"/>:
/// <c>SHCreateItemFromParsingName</c> → <c>IShellItemImageFactory::GetImage</c> → HBITMAP pixel copy) that replaced the
/// inspector's former WinRT <c>StorageFile.GetThumbnailAsync</c> path. This is the per-selection cost the thumbnail
/// handler now pays, measured in isolation from the messaging/handler plumbing covered by
/// <c>ThumbnailHandlerBenchmarks</c>.
/// </summary>
/// <remarks>
/// <para>
/// The migration's goal was deterministic COM lifetime: every COM object the path creates (the shell item, the image
/// factory, the HBITMAP) is released before the call returns, so no runtime-callable wrapper survives to the finalizer.
/// The expected <see cref="NativeMemoryProfiler"/> "native memory leak" delta is therefore flat; a growing delta would
/// mean a COM object or GDI handle is escaping release on this path. The <see cref="MemoryDiagnoserAttribute"/>
/// <c>Allocated</c> column reflects the one deterministically-released RCW per file plus the copied BGRA pixel buffer.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("Interop", "Thumbnail")]
// ReSharper disable once ClassCanBeSealed.Global
public class ShellThumbnailInteropBenchmarks
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
            nameof(ShellThumbnailInteropBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _paths = CreateImageFiles("png", FileCount);
        _container = CreateContainer();
        _shellThumbnails = _container.Resolve<IShellThumbnailInterop>();
    }

    /// <summary>
    /// Extracts each thumbnail straight from its path via the Win32 Shell imaging COM path, summing the copied
    /// pixel-buffer lengths so the work is not optimized away.
    /// </summary>
    [Benchmark]
    public ulong ExtractThumbnails()
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
