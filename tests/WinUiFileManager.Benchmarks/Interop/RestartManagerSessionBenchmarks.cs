using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.Interop;

/// <summary>
/// Confirms that the full Restart Manager session lifecycle (<c>RmStartSession</c> → <c>RmRegisterResources</c> →
/// <c>RmGetList</c> → <c>RmEndSession</c>) releases its native session state and leaks no native memory under repeated
/// use. <c>InspectorLocksDiagnosticsHandler</c> runs this per-file cycle when probing which processes hold a lock.
/// </summary>
/// <remarks>
/// <para>
/// The Restart Manager keeps an OS-side session alive until <c>RmEndSession</c> is called (see
/// <see cref="IRestartManagerInterop"/>'s lifetime contract), so a missing or failing <c>EndSession</c> is the most
/// plausible native leak on this path. This benchmark opens and closes one session per file to stress
/// create/destroy. The benchmark files are never locked, so <c>RmGetList</c> reports zero processes and only the
/// count-probe phase runs.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> a flat <see cref="NativeMemoryProfiler"/> "native memory leak" delta
/// confirms <c>EndSession</c> reclaims the session. A delta that scales with <c>FileCount</c> × iterations would
/// indicate sessions are not being released.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("Interop")]
// ReSharper disable once ClassCanBeSealed.Global
public class RestartManagerSessionBenchmarks
{
    [Params(10, 50)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private IContainer? _container;
    private IRestartManagerInterop? _restartManager;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(RestartManagerSessionBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateFiles("files", FileCount);
        _container = CreateContainer();
        _restartManager = _container.Resolve<IRestartManagerInterop>();
    }

    /// <summary>
    /// Runs one full session lifecycle per file (start, register, count-probe, end), summing the Win32 result codes
    /// and the reported process counts so the calls are not optimized away. A session that starts is always ended.
    /// </summary>
    [Benchmark]
    public long SessionLifecycle()
    {
        var restartManager = _restartManager ?? throw new InvalidOperationException("Benchmark Restart Manager is not initialized.");
        long total = 0;

        foreach (var path in _filePaths)
        {
            if (restartManager.StartSession(out var sessionHandle) != 0)
            {
                continue;
            }

            try
            {
                total += restartManager.RegisterResources(sessionHandle, [path]);

                uint processInfo = 0;
                total += restartManager.GetList(sessionHandle, out var processInfoNeeded, ref processInfo, null, out _);
                total += processInfoNeeded;
            }
            finally
            {
                restartManager.EndSession(sessionHandle);
            }
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container?.Dispose();
        _container = null;
        _restartManager = null;
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
