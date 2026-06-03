namespace WinUiFileManager.Benchmarks.SecurityHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class SecurityHandlerBenchmarks
{
    [Params(500, 2000)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _files = [];
    private InspectorDiagnosticsRequestMessage[] _requests = [];

    private readonly Channel<int> _responses = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });

    private IContainer? _container;
    private IMessenger? _messenger;
    private IDisposable? _responseSubscription;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(SecurityHandlerBenchmarks));

        if (Directory.Exists(_benchmarkDirectory))
        {
            Directory.Delete(_benchmarkDirectory, recursive: true);
        }

        Directory.CreateDirectory(_benchmarkDirectory);
        _files = CreateFiles(FileCount);
        _requests = _files
            .Select(static filePath => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(filePath)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorSecurityDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _responseSubscription = _messenger
            .CreateObservable<InspectorSecurityDiagnosticsResponseMessage>()
            .Subscribe(OnResponse);
    }

    [Benchmark]
    public async Task<int> InspectorSecurityDiagnosticsHandler()
    {
        var total = 0;

        foreach (var request in _requests)
        {
            Messenger.Send(request);
            total += await _responses.Reader.ReadAsync().ConfigureAwait(false);
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _responseSubscription?.Dispose();
        _responseSubscription = null;
        _container?.Dispose();
        _container = null;
        _messenger = null;
        _files = [];
        _requests = [];

        if (Directory.Exists(_benchmarkDirectory))
        {
            Directory.Delete(_benchmarkDirectory, recursive: true);
        }
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

    private string[] CreateFiles(int fileCount)
    {
        var filePaths = new string[fileCount];

        for (var i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(_benchmarkDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, string.Empty);
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private void OnResponse(InspectorSecurityDiagnosticsResponseMessage response)
    {
        _responses.Writer.TryWrite(response.Diagnostics.Owner.Length);
    }
}
