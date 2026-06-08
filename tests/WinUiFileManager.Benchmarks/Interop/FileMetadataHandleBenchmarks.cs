using Microsoft.Win32.SafeHandles;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.Interop;

/// <summary>
/// Confirms that the open-handle / read-metadata / dispose cycle on <see cref="IFileSystemMetadataInterop"/> leaks no
/// native handle memory. <c>InspectorIdentityDiagnosticsHandler</c> performs this exact cycle
/// (<c>OpenForMetadataRead</c> → <c>TryGet*</c> → dispose the <c>SafeFileHandle</c>) on every file inspection.
/// </summary>
/// <remarks>
/// <para>
/// The metadata handle is a kernel file handle owned by a <c>SafeFileHandle</c> the caller disposes, so release is
/// deterministic rather than finalizer-dependent. The expected <see cref="NativeMemoryProfiler"/> "native memory
/// leak" delta is flat; a growing delta would mean a handle is escaping disposal somewhere on this path.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("Interop")]
// ReSharper disable once ClassCanBeSealed.Global
public class FileMetadataHandleBenchmarks
{
    [Params(20, 100)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private IContainer? _container;
    private IFileSystemMetadataInterop? _metadata;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(FileMetadataHandleBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateFiles("files", FileCount);
        _container = CreateContainer();
        _metadata = _container.Resolve<IFileSystemMetadataInterop>();
    }

    /// <summary>
    /// Opens a metadata handle for each file, reads basic info, and disposes the handle, summing attribute values so
    /// the work is not optimized away.
    /// </summary>
    [Benchmark]
    public long OpenReadDisposeMetadataHandle()
    {
        var metadata = _metadata ?? throw new InvalidOperationException("Benchmark metadata reader is not initialized.");
        long total = 0;

        foreach (var path in _filePaths)
        {
            using SafeFileHandle handle = metadata.OpenForMetadataRead(path, treatAsDirectory: false);
            if (metadata.TryGetFileBasicInfo(handle, out var info))
            {
                total += info.FileAttributes;
            }
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container?.Dispose();
        _container = null;
        _metadata = null;
        _filePaths = [];
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

    private string[] CreateFiles(string groupName, int count)
    {
        var directory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(directory);

        var paths = new string[count];
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(directory, $"file-{i:D6}.bin");
            File.WriteAllText(path, $"content-{i}");
            paths[i] = path;
        }

        return paths;
    }
}
