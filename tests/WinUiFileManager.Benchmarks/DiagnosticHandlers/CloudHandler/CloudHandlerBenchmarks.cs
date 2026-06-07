using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.DiagnosticHandlers.CloudHandler;

/// <summary>
/// End-to-end native-memory benchmark for <c>InspectorCloudDiagnosticsHandler</c>. A
/// <see cref="BenchmarkSyncRootRegistryReader"/> is registered over the real registry reader so the benchmark's temp
/// directory is treated as a registered sync root; this forces the handler's WinRT branch
/// (<c>StorageFile/StorageFolder.GetFromPathAsync</c> → <c>Provider</c> → <c>Properties.RetrievePropertiesAsync</c>)
/// to run for every request. Without the override the registry has no sync root for the temp path, the handler
/// short-circuits, and the COM-allocating path this benchmark targets is never measured.
/// </summary>
/// <remarks>
/// <c>FileCount</c> is kept modest because each request now performs several real WinRT/Shell calls; the
/// <see cref="NativeMemoryProfiler"/> native-byte delta is the signal of interest.
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("DiagnosticHandlers")]
// ReSharper disable once ClassCanBeSealed.Global
public class CloudHandlerBenchmarks
{
    private const FileAttributes FileAttributePinned = (FileAttributes)0x00080000;
    private const FileAttributes FileAttributeUnpinned = (FileAttributes)0x00100000;
    private const FileAttributes FileAttributeRecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes FileAttributeRecallOnDataAccess = (FileAttributes)0x00400000;

    [Params(10, 50)]
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
        _messenger.Register<CloudHandlerBenchmarks, InspectorCloudDiagnosticsResponseMessage>(
            this,
            static (benchmark, response) => benchmark.OnResponse(response));
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
        _messenger?.UnregisterAll(this);
        _container?.Dispose();
        _container = null;
        _messenger = null;
        _localFilePaths = [];
        _cloudAttributeFilePaths = [];
        _directoryPaths = [];
        _requests = [];

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();

        // Override the real registry reader (registered last wins in Autofac) so the handler treats the benchmark
        // directory as a sync root and exercises its WinRT/Shell branch on every request.
        builder.RegisterInstance(new BenchmarkSyncRootRegistryReader(_benchmarkDirectory))
            .As<ISyncRootRegistryReader>();

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
