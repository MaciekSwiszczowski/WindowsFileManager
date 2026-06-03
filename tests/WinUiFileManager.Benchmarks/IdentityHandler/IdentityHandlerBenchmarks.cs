namespace WinUiFileManager.Benchmarks.IdentityHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class IdentityHandlerBenchmarks
{
    [Params(500, 2000)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private string[] _directoryPaths = [];
    private string[] _deepFilePaths = [];
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
            nameof(IdentityHandlerBenchmarks));

        if (Directory.Exists(_benchmarkDirectory))
        {
            Directory.Delete(_benchmarkDirectory, recursive: true);
        }

        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateRegularFiles("files", FileCount);
        _directoryPaths = CreateDirectories("directories", FileCount);
        _deepFilePaths = CreateDeepNestedFiles("deep-path", FileCount);

        _requests = _filePaths
            .Concat(_directoryPaths)
            .Concat(_deepFilePaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorIdentityDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _responseSubscription = _messenger
            .CreateObservable<InspectorIdentityDiagnosticsResponseMessage>()
            .Subscribe(OnResponse);
    }

    [Benchmark]
    public async Task<int> InspectorIdentityDiagnosticsHandler()
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
        _filePaths = [];
        _directoryPaths = [];
        _deepFilePaths = [];
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

    private string[] CreateRegularFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];
        var utcNow = DateTime.UtcNow;

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllBytes(filePath, new byte[i % 256 + 1]);

            var attributes = (FileAttributes)((i % 3) switch
            {
                0 => FileAttributes.ReadOnly,
                1 => FileAttributes.Hidden,
                _ => FileAttributes.Archive,
            });
            File.SetAttributes(filePath, attributes);
            File.SetCreationTimeUtc(filePath, utcNow.AddMinutes(-i));
            File.SetLastWriteTimeUtc(filePath, utcNow.AddSeconds(-i));

            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private string[] CreateDirectories(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var directoryPaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var directoryPath = Path.Combine(groupDirectory, $"dir-{i:D6}");
            Directory.CreateDirectory(directoryPath);
            directoryPaths[i] = directoryPath;
        }

        return directoryPaths;
    }

    private string[] CreateDeepNestedFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var nestedDirectory = Path.Combine(groupDirectory, $"segment-{i % 8:D2}", $"nested-{i:D6}");
            Directory.CreateDirectory(nestedDirectory);
            var filePath = Path.Combine(nestedDirectory, "target.bin");
            File.WriteAllText(filePath, new string('x', (i % 64) + 1));
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private void OnResponse(InspectorIdentityDiagnosticsResponseMessage response)
    {
        var score = (int)response.Diagnostics.NtfsMetadata.Attributes
                    + response.Diagnostics.Identity.FinalPath.Length
                    + response.Diagnostics.Identity.FileId.Bytes.Length;
        _responses.Writer.TryWrite(score);
    }
}
