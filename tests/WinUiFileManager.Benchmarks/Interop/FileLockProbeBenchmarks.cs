using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.Interop;

/// <summary>
/// Confirms that <see cref="FileLockProbeInterop.TryOpenExclusively"/> — which opens each probed file via
/// <c>CreateFileW</c> wrapped in a <c>SafeFileHandle</c> and disposes it with <c>using</c> — leaks no native handle
/// memory under repeated probing. This is the hot path behind the locks diagnostics' "is the file in use?" check.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the WinRT storage benchmarks, the native resource here is a kernel file handle owned by a
/// <c>SafeHandle</c>, whose release is deterministic (the <c>using</c> in the adapter), not finalizer-dependent. The
/// expected <see cref="NativeMemoryProfiler"/> "native memory leak" delta is therefore flat and near-zero; a growing
/// delta would indicate a handle that escapes the <c>SafeHandle</c> contract.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("Interop")]
// ReSharper disable once ClassCanBeSealed.Global
public class FileLockProbeBenchmarks
{
    [Params(20, 100)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private IContainer? _container;
    private FileLockProbeInterop? _probe;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(FileLockProbeBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateFiles("files", FileCount);
        _container = CreateContainer();
        _probe = _container.Resolve<FileLockProbeInterop>();
    }

    /// <summary>
    /// Probes each file for an exclusive open, summing the returned Win32 codes so the calls are not optimized away.
    /// The benchmark files are never locked, so each probe succeeds, opens a handle, and disposes it.
    /// </summary>
    [Benchmark]
    public long ProbeExclusiveOpen()
    {
        var probe = _probe ?? throw new InvalidOperationException("Benchmark probe is not initialized.");
        long total = 0;

        foreach (var path in _filePaths)
        {
            total += probe.TryOpenExclusively(path);
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container?.Dispose();
        _container = null;
        _probe = null;
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
