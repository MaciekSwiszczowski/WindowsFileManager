using System.Security.AccessControl;
using System.Security.Principal;

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
    private string[] _inheritedFilePaths = [];
    private string[] _explicitAclFilePaths = [];
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
            nameof(SecurityHandlerBenchmarks));

        if (Directory.Exists(_benchmarkDirectory))
        {
            Directory.Delete(_benchmarkDirectory, recursive: true);
        }

        Directory.CreateDirectory(_benchmarkDirectory);

        _inheritedFilePaths = CreateInheritedFiles("inherited", FileCount);
        _explicitAclFilePaths = CreateExplicitAclFiles("explicit-acl", FileCount);
        _directoryPaths = CreateDirectories("directories", FileCount);

        _requests = _inheritedFilePaths
            .Concat(_explicitAclFilePaths)
            .Concat(_directoryPaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
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
        _inheritedFilePaths = [];
        _explicitAclFilePaths = [];
        _directoryPaths = [];
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

    private string[] CreateInheritedFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, string.Empty);
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private string[] CreateExplicitAclFiles(string groupName, int count)
    {
        var groupDirectory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(groupDirectory);

        var filePaths = new string[count];
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(groupDirectory, $"file-{i:D6}.bin");
            File.WriteAllText(filePath, string.Empty);

            var fileInfo = new FileInfo(filePath);
            var security = fileInfo.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.ReadData,
                AccessControlType.Deny));
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.WriteData,
                AccessControlType.Allow));
            fileInfo.SetAccessControl(security);

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

    private void OnResponse(InspectorSecurityDiagnosticsResponseMessage response)
    {
        var score = response.Diagnostics.Owner.Length
                    + response.Diagnostics.DaclSummary.Length
                    + response.Diagnostics.SaclSummary.Length;
        _responses.Writer.TryWrite(score);
    }
}
