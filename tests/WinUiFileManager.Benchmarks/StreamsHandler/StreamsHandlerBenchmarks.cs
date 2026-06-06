namespace WinUiFileManager.Benchmarks.StreamsHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class StreamsHandlerBenchmarks
{
    [Params(100, 500)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filesWithoutStreams = [];
    private string[] _filesWithOneStream = [];
    private string[] _filesWithTwoStreams = [];
    private InspectorDiagnosticsRequestMessage[] _requests = [];

    // Long-lived async hand-off between the (thread-pool) response publisher and the benchmark loop.
    // The loop is strictly serial (send -> await -> send), so the channel never buffers more than one
    // item and steady-state allocation is effectively zero. This is the template for further
    // message-round-trip benchmarks under [MemoryDiagnoser].
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
            nameof(StreamsHandlerBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filesWithoutStreams = CreateFiles("no-streams", FileCount);
        _filesWithOneStream = CreateFiles("one-stream", FileCount);
        _filesWithTwoStreams = CreateFiles("two-streams", FileCount);

        AddAlternateStreams(_filesWithOneStream, streamCount: 1, streamSize: 1000);
        AddAlternateStreams(_filesWithTwoStreams, streamCount: 2, streamSize: 2000);

        _requests = _filesWithoutStreams
            .Concat(_filesWithOneStream)
            .Concat(_filesWithTwoStreams)
            .Select(static filePath => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(filePath)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorStreamsDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _messenger.Register<StreamsHandlerBenchmarks, InspectorStreamsDiagnosticsResponseMessage>(
            this,
            static (benchmark, response) => benchmark.OnResponse(response));
    }

    [Benchmark]
    public async Task<int> InspectorStreamsDiagnosticsHandler()
    {
        var streamCount = 0;

        foreach (var request in _requests)
        {
            Messenger.Send(request);
            streamCount += await _responses.Reader.ReadAsync().ConfigureAwait(false);
        }

        return streamCount;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _messenger?.UnregisterAll(this);
        _container?.Dispose();
        _container = null;
        _messenger = null;
        _filesWithoutStreams = [];
        _filesWithOneStream = [];
        _filesWithTwoStreams = [];
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

    private string[] CreateFiles(string groupName, int fileCount)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[fileCount];

        for (var i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, string.Empty);
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private static void AddAlternateStreams(string[] filePaths, int streamCount, int streamSize)
    {
        var streamContent = new byte[streamSize];

        foreach (var filePath in filePaths)
        {
            for (var streamIndex = 1; streamIndex <= streamCount; streamIndex++)
            {
                File.WriteAllBytes($"{filePath}:stream-{streamIndex}", streamContent);
            }
        }
    }

    private void OnResponse(InspectorStreamsDiagnosticsResponseMessage response)
    {
        // Unbounded + single-writer: always succeeds without allocating in steady state.
        _responses.Writer.TryWrite(response.Diagnostics.AlternateStreamCount);
    }
}
