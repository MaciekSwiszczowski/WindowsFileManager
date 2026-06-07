using Windows.Storage.Provider;

namespace WinUiFileManager.Benchmarks.SyncRootEnumeration;

/// <summary>
/// Measures the allocation cost of <see cref="StorageProviderSyncRootManager.GetCurrentSyncRoots"/>,
/// the WinRT call that <c>StorageProviderSyncRootCache</c> makes on every cache miss.
/// </summary>
/// <remarks>
/// <para>
/// The cache has a 30-second TTL specifically to amortize this call: each invocation enumerates all
/// registered cloud-provider sync roots and allocates a <see cref="StorageProviderSyncRootInfo"/> WinRT
/// object per root. Without the cache, every cloud-file inspection would pay this cost independently.
/// </para>
/// <para>
/// <strong>Reading the results:</strong>
/// <list type="bullet">
///   <item><c>EnumerationCount = 1</c> — baseline cost of a single cache miss.</item>
///   <item><c>EnumerationCount = 10</c> — cost of ten back-to-back misses, representing what would
///   happen without the cache when ten cloud-attribute files are inspected in rapid succession.</item>
/// </list>
/// The managed-allocation column from <see cref="MemoryDiagnoserAttribute"/> shows how many
/// <see cref="StorageProviderSyncRootInfo"/> objects are created per enumeration pass. The native-byte
/// delta from <see cref="NativeMemoryProfiler"/> shows whether the WinRT provider-registration objects
/// retain native memory after the managed references are dropped.
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

    /// <summary>
    /// Simulates <c>StorageProviderSyncRootCache.LoadSyncRoots()</c>: calls
    /// <see cref="StorageProviderSyncRootManager.GetCurrentSyncRoots"/> the requested number of times,
    /// extracting only the string fields (path, id) that the cache snapshots, and discards the WinRT
    /// objects — mirroring the cache's design of storing managed strings rather than live WinRT objects.
    /// </summary>
    [Benchmark]
    public int EnumerateSyncRoots()
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
}
