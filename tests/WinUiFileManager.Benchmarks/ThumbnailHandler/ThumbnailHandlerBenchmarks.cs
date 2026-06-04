namespace WinUiFileManager.Benchmarks.ThumbnailHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class ThumbnailHandlerBenchmarks
{
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

    private static readonly byte[] MinimalJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
        0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
        0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
        0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
        0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
        0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
        0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
        0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x08, 0xFF, 0xC4, 0x00, 0x14, 0x10, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
        0x7F, 0x80, 0xFF, 0xD9,
    ];

    [Params(100, 500)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _textFilePaths = [];
    private string[] _pngFilePaths = [];
    private string[] _jpegFilePaths = [];
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
            nameof(ThumbnailHandlerBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _textFilePaths = CreateTextFiles("text", FileCount);
        _pngFilePaths = CreateImageFiles("png", FileCount, ".png", MinimalPng);
        _jpegFilePaths = CreateImageFiles("jpeg", FileCount, ".jpg", MinimalJpeg);
        _directoryPaths = CreateThumbnailDirectories("directories", FileCount);

        _requests = _textFilePaths
            .Concat(_pngFilePaths)
            .Concat(_jpegFilePaths)
            .Concat(_directoryPaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorThumbnailDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _responseSubscription = _messenger
            .CreateObservable<InspectorThumbnailDiagnosticsResponseMessage>()
            .Subscribe(OnResponse);
    }

    [Benchmark]
    public async Task<int> InspectorThumbnailDiagnosticsHandler()
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
        _textFilePaths = [];
        _pngFilePaths = [];
        _jpegFilePaths = [];
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

    private string[] CreateTextFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.txt");
            File.WriteAllText(filePath, new string('a', (i % 128) + 1));
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private string[] CreateImageFiles(string groupName, int count, string extension, byte[] content)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}{extension}");
            File.WriteAllBytes(filePath, content);
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private string[] CreateThumbnailDirectories(string groupName, int count)
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

    private void OnResponse(InspectorThumbnailDiagnosticsResponseMessage response)
    {
        var score = (response.Diagnostics.ThumbnailBytes?.Length ?? 0)
                    + response.Diagnostics.ProgId.Length;
        _responses.Writer.TryWrite(score);
    }
}
