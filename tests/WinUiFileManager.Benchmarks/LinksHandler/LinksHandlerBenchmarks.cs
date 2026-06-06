namespace WinUiFileManager.Benchmarks.LinksHandler;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class LinksHandlerBenchmarks
{
    [Params(100, 500)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _plainFilePaths = [];
    private string[] _symlinkPaths = [];
    private string[] _shortcutPaths = [];
    private string[] _junctionPaths = [];
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
            nameof(LinksHandlerBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        var plainDirectory = Path.Combine(_benchmarkDirectory, "plain");
        Directory.CreateDirectory(plainDirectory);
        _plainFilePaths = CreatePlainFiles(plainDirectory, FileCount);

        _symlinkPaths = CreateSymlinks(
            Path.Combine(_benchmarkDirectory, "symlink"),
            _plainFilePaths,
            FileCount);

        _shortcutPaths = CreateShortcutExtensionFiles(
            Path.Combine(_benchmarkDirectory, "shell-shortcut"),
            FileCount);

        _junctionPaths = CreateDirectoryJunctions(
            Path.Combine(_benchmarkDirectory, "junction"),
            plainDirectory,
            FileCount);

        _requests = _plainFilePaths
            .Concat(_symlinkPaths)
            .Concat(_shortcutPaths)
            .Concat(_junctionPaths)
            .Select(static path => new InspectorDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(path)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorLinksDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _messenger.Register<LinksHandlerBenchmarks, InspectorLinksDiagnosticsResponseMessage>(
            this,
            static (benchmark, response) => benchmark.OnResponse(response));
    }

    [Benchmark]
    public async Task<int> InspectorLinksDiagnosticsHandler()
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
        _plainFilePaths = [];
        _symlinkPaths = [];
        _shortcutPaths = [];
        _junctionPaths = [];
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

    private static string[] CreatePlainFiles(string directory, int count)
    {
        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(directory, $"target-{i:D6}.txt");
            File.WriteAllText(filePath, $"plain-{i}");
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private static string[] CreateSymlinks(string directory, string[] targets, int count)
    {
        Directory.CreateDirectory(directory);
        var linkPaths = new string[count];
        var useSymlinks = true;

        for (var i = 0; i < count; i++)
        {
            var linkPath = Path.Combine(directory, $"link-{i:D6}.txt");

            if (useSymlinks)
            {
                try
                {
                    var target = targets[i % targets.Length];
                    if (File.Exists(linkPath))
                    {
                        File.Delete(linkPath);
                    }

                    File.CreateSymbolicLink(linkPath, target);
                    linkPaths[i] = linkPath;
                    continue;
                }
                catch
                {
                    useSymlinks = false;
                }
            }

            linkPaths[i] = targets[i % targets.Length];
        }

        return linkPaths;
    }

    private static string[] CreateShortcutExtensionFiles(string directory, int count)
    {
        Directory.CreateDirectory(directory);
        var filePaths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var filePath = Path.Combine(directory, $"shortcut-{i:D6}.lnk");
            File.WriteAllText(filePath, $"shortcut-placeholder-{i}");
            filePaths[i] = filePath;
        }

        return filePaths;
    }

    private static string[] CreateDirectoryJunctions(string directory, string targetDirectory, int count)
    {
        Directory.CreateDirectory(directory);
        var junctionPaths = new string[count];
        var created = 0;

        for (var i = 0; i < count; i++)
        {
            var junctionPath = Path.Combine(directory, $"junction-{i:D6}");
            junctionPaths[i] = junctionPath;

            if (created < 0)
            {
                Directory.CreateDirectory(junctionPath);
                continue;
            }

            try
            {
                if (Directory.Exists(junctionPath))
                {
                    Directory.Delete(junctionPath);
                }

                Directory.CreateSymbolicLink(junctionPath, targetDirectory);
                created++;
            }
            catch
            {
                Directory.CreateDirectory(junctionPath);
                created = -1;
            }
        }

        return junctionPaths;
    }

    private void OnResponse(InspectorLinksDiagnosticsResponseMessage response)
    {
        var score = response.Diagnostics.LinkTarget.Length
                    + response.Diagnostics.LinkStatus.Length
                    + response.Diagnostics.ReparseTag.Length;
        _responses.Writer.TryWrite(score);
    }
}
