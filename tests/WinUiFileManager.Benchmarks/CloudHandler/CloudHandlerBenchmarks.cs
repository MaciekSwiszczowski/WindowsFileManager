namespace WinUiFileManager.Benchmarks.CloudHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class CloudHandlerBenchmarks
{
    private const FileAttributes FileAttributePinned = (FileAttributes)0x00080000;
    private const FileAttributes FileAttributeUnpinned = (FileAttributes)0x00100000;
    private const FileAttributes FileAttributeRecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes FileAttributeRecallOnDataAccess = (FileAttributes)0x00400000;

    [Params(2, 10)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _localFilePaths = [];
    private string[] _cloudAttributeFilePaths = [];
    private string[] _directoryPaths = [];
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
            nameof(CloudHandlerBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _localFilePaths = CreateLocalFiles("local", FileCount);
        _cloudAttributeFilePaths = CreateCloudAttributeFiles("cloud-attributes", FileCount);
        _directoryPaths = CreateDirectories("directories", FileCount);

        _requests = _localFilePaths
            .Concat(_cloudAttributeFilePaths)
            .Concat(_directoryPaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorCloudDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _responseSubscription = _messenger
            .CreateObservable<InspectorCloudDiagnosticsResponseMessage>()
            .Subscribe(OnResponse);
    }

    [Benchmark]
    public async Task<int> InspectorCloudDiagnosticsHandler()
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
        _localFilePaths = [];
        _cloudAttributeFilePaths = [];
        _directoryPaths = [];
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

    private string[] CreateLocalFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, $"local-{i}");
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private string[] CreateCloudAttributeFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, $"cloud-attr-{i}");

            var attributes = File.GetAttributes(filePath);
            attributes |= (i % 4) switch
            {
                0 => FileAttributes.Offline | FileAttributePinned,
                1 => FileAttributeUnpinned | FileAttributeRecallOnOpen,
                2 => FileAttributeRecallOnDataAccess,
                _ => FileAttributes.Normal,
            };
            File.SetAttributes(filePath, attributes);

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

    private void OnResponse(InspectorCloudDiagnosticsResponseMessage response)
    {
        var score = (response.Diagnostics.IsCloudControlled ? 1000 : 0)
                    + response.Diagnostics.Status.Length
                    + response.Diagnostics.Provider.Length;
        _responses.Writer.TryWrite(score);
    }
}
