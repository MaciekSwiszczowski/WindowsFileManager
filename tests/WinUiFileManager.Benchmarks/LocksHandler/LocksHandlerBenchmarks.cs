namespace WinUiFileManager.Benchmarks.LocksHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class LocksHandlerBenchmarks
{
    [Params(10, 25)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _unlockedFilePaths = [];
    private string[] _lockedFilePaths = [];
    private InspectorDiagnosticsRequestMessage[] _requests = [];
    private readonly List<FileStream> _lockHolders = [];

    private readonly Channel<int> _responses = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });

    private IContainer? _container;
    private IMessenger? _messenger;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(LocksHandlerBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _unlockedFilePaths = CreateFiles("unlocked", FileCount);
        _lockedFilePaths = CreateFiles("locked", FileCount);

        _requests = _unlockedFilePaths
            .Concat(_lockedFilePaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorLocksDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _messenger.Register<LocksHandlerBenchmarks, InspectorLocksDiagnosticsResponseMessage>(
            this,
            static (benchmark, response) => benchmark.OnResponse(response));
    }

    [IterationSetup]
    public void IterationSetup()
    {
        HoldExclusiveLocks(_lockedFilePaths);
    }

    [Benchmark]
    public async Task<int> InspectorLocksDiagnosticsHandler()
    {
        var total = 0;

        foreach (var request in _requests)
        {
            Messenger.Send(request);
            total += await _responses.Reader.ReadAsync().ConfigureAwait(false);
        }

        return total;
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        ReleaseExclusiveLocks();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _messenger?.UnregisterAll(this);

        ReleaseExclusiveLocks();
        _container?.Dispose();
        _container = null;
        _messenger = null;
        _unlockedFilePaths = [];
        _lockedFilePaths = [];
        _requests = [];

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private static IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();

        return builder.Build();
    }

    private IMessenger Messenger =>
        _messenger ?? throw new InvalidOperationException("Benchmark messenger is not initialized.");

    private string[] CreateFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllBytes(filePath, new byte[64]);
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    /// <summary>
    /// Holds benchmark files locked only for the measured iteration.
    /// </summary>
    /// <remarks>
    /// Do not move these handles back to <see cref="GlobalSetup"/>. The native memory profiler intentionally stays
    /// enabled for these benchmarks, and handles that survive the measured iteration can be reported as native
    /// leaks even though <see cref="GlobalCleanup"/> would release them later.
    /// </remarks>
    private void HoldExclusiveLocks(string[] filePaths)
    {
        foreach (var filePath in filePaths)
        {
            _lockHolders.Add(new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None));
        }
    }

    private void ReleaseExclusiveLocks()
    {
        foreach (var stream in _lockHolders)
        {
            stream.Dispose();
        }

        _lockHolders.Clear();
    }

    private void OnResponse(InspectorLocksDiagnosticsResponseMessage response)
    {
        var score = (response.Diagnostics.InUse == true ? 100 : 0)
                    + response.Diagnostics.LockBy.Count
                    + response.Diagnostics.LockPids.Count;
        _responses.Writer.TryWrite(score);
    }
}
