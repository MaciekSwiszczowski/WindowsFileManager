using Windows.Storage.Provider;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Benchmarks.SyncRootEnumeration;

/// <summary>
/// Validates the fix for the sync-root enumeration native-memory leak by comparing the new registry-backed
/// reader (<see cref="ISyncRootRegistryReader"/>, what <c>StorageProviderSyncRootCache</c> now uses) against the
/// WinRT <see cref="StorageProviderSyncRootManager.GetCurrentSyncRoots"/> enumeration it replaced.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReadViaWinRt"/> is the retained "before" baseline: each call enumerates all registered sync roots
/// and allocates a COM-backed <see cref="StorageProviderSyncRootInfo"/> per root, whose native memory is reclaimed
/// only on GC finalization — the original benchmark showed this leaking ~50 KB of native memory per call, scaling
/// linearly with <see cref="EnumerationCount"/>. <see cref="ReadViaRegistry"/> is the production path: it reads the
/// same fields from the registry into plain records with no native footprint.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> the <see cref="NativeMemoryProfiler"/> "native memory leak" column should
/// be flat and near-zero for <see cref="ReadViaRegistry"/> regardless of <see cref="EnumerationCount"/>, versus the
/// linear growth of <see cref="ReadViaWinRt"/>. The baseline ratio on <see cref="ReadViaRegistry"/> also shows the
/// throughput gain from dropping the WinRT provider enumeration.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("WinRT", "SyncRoot")]
// ReSharper disable once ClassCanBeSealed.Global
public class SyncRootEnumerationBenchmarks
{
    [Params(1, 10)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int EnumerationCount { get; set; }

    private IContainer? _container;
    private ISyncRootRegistryReader? _reader;

    [GlobalSetup]
    public void Setup()
    {
        _container = CreateContainer();
        _reader = _container.Resolve<ISyncRootRegistryReader>();
    }

    /// <summary>
    /// Production path: reads registered sync roots from the registry the requested number of times, summing the
    /// path/id lengths so the read is not optimized away.
    /// </summary>
    [Benchmark]
    public int ReadViaRegistry()
    {
        var reader = _reader ?? throw new InvalidOperationException("Benchmark reader is not initialized.");
        var total = 0;

        for (var i = 0; i < EnumerationCount; i++)
        {
            foreach (var registration in reader.ReadRegisteredSyncRoots())
            {
                total += registration.Path.Length + registration.Id.Length;
            }
        }

        return total;
    }

    /// <summary>
    /// Retained "before" baseline: the WinRT enumeration that previously backed the cache, kept only to quantify the
    /// leak and throughput difference the registry reader fixes. Not used by production code anymore.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int ReadViaWinRt()
    {
        var total = 0;

        for (var i = 0; i < EnumerationCount; i++)
        {
            total += StorageProviderSyncRootManager
                .GetCurrentSyncRoots()
                .Sum(static syncRoot => (syncRoot.Path?.Path?.Length ?? 0) + (syncRoot.Id?.Length ?? 0));
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _container?.Dispose();
        _container = null;
        _reader = null;
    }

    private static IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.AddInfrastructureServices();

        return builder.Build();
    }
}
