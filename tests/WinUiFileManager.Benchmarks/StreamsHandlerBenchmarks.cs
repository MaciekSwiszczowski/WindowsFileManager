using Autofac;
using Autofac.Extensions.DependencyInjection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Diagnostics.Inspector;
using WinUiFileManager.Infrastructure;

namespace WinUiFileManager.Benchmarks;

[MemoryDiagnoser]
[NativeMemoryProfiler]
// ReSharper disable once ClassCanBeSealed.Global
public class StreamsHandlerBenchmarks
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(30);

    [Params(1_000)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private InspectorStreamsDiagnosticsRequestMessage[] _requests = [];
    private readonly ManualResetEventSlim _responseReceived = new();
    private IContainer? _container;
    private IMessenger? _messenger;
    private IDisposable? _responseSubscription;
    private int _lastAlternateStreamCount;

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(StreamsHandlerBenchmarks));

        if (Directory.Exists(_benchmarkDirectory))
        {
            Directory.Delete(_benchmarkDirectory, recursive: true);
        }

        Directory.CreateDirectory(_benchmarkDirectory);

        for (var i = 0; i < FileCount; i++)
        {
            File.WriteAllText(Path.Combine(_benchmarkDirectory, $"file-{i:D6}.bin"), string.Empty);
        }

        _filePaths = Directory.GetFiles(_benchmarkDirectory);
        _requests = _filePaths
            .Select(static filePath => new InspectorStreamsDiagnosticsRequestMessage(
                NormalizedPath.FromFullyQualifiedPath(filePath)))
            .ToArray();

        _container = CreateContainer();
        _container.Resolve<InspectorStreamsDiagnosticsHandler>().Initialize();
        _messenger = _container.Resolve<IMessenger>();
        _responseSubscription = _messenger
            .CreateObservable<InspectorStreamsDiagnosticsResponseMessage>()
            .Subscribe(OnResponse);
    }

    [Benchmark]
    public int InspectorStreamsDiagnosticsHandler()
    {
        var streamCount = 0;

        foreach (var request in _requests)
        {
            _lastAlternateStreamCount = 0;
            _responseReceived.Reset();

            _messenger?.Send(request);

            if (!_responseReceived.Wait(ResponseTimeout))
            {
                throw new TimeoutException("Timed out waiting for inspector streams diagnostics response.");
            }

            streamCount += _lastAlternateStreamCount;
        }

        return streamCount;
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
        _requests = [];
        _responseReceived.Reset();

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

    private void OnResponse(InspectorStreamsDiagnosticsResponseMessage response)
    {
        _lastAlternateStreamCount = int.TryParse(
            response.Diagnostics.AlternateStreamCount,
            out var alternateStreamCount)
            ? alternateStreamCount
            : 0;

        _responseReceived.Set();
    }
}
