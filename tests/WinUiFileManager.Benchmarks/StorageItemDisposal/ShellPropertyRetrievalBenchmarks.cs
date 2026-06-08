using Windows.Storage;

namespace WinUiFileManager.Benchmarks.StorageItemDisposal;

/// <summary>
/// Isolates the native cost of the Shell property store (<c>StorageItemContentProperties.RetrievePropertiesAsync</c>)
/// from the cost of acquiring the <see cref="StorageFile"/> itself. <c>InspectorCloudDiagnosticsHandler</c> calls
/// <c>RetrievePropertiesAsync</c> for the sync properties on every cloud-file inspection.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="StorageFile"/> objects are acquired once in <see cref="Setup"/> and reused across all iterations,
/// so the per-iteration measurement reflects <em>only</em> repeated property-store retrieval, not RCW acquisition
/// (which <see cref="StorageItemAccumulationBenchmarks"/> already covers). Each <c>RetrievePropertiesAsync</c> call
/// opens a Shell <c>IPropertyStore</c> under the hood — native COM that, like the storage items themselves, is
/// reclaimed only by GC finalization.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> a <see cref="NativeMemoryProfiler"/> "native memory leak" delta that climbs
/// with iterations — even though the storage items are fixed — points to the property store, not the
/// <see cref="StorageFile"/>, as the dominant retained native allocation on the cloud-inspection path.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("WinRT", "StorageItem")]
// ReSharper disable once ClassCanBeSealed.Global
public class ShellPropertyRetrievalBenchmarks
{
    private static readonly string[] SyncProperties =
    [
        "System.Sync.State",
        "System.SyncTransferStatus",
        "System.Sync.Status",
    ];

    [Params(10, 50)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private StorageFile[] _files = [];

    [GlobalSetup]
    public async Task Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(ShellPropertyRetrievalBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        var paths = CreateFiles("files", FileCount);
        _files = new StorageFile[paths.Length];
        for (var i = 0; i < paths.Length; i++)
        {
            _files[i] = await StorageFile.GetFileFromPathAsync(paths[i]);
        }
    }

    /// <summary>
    /// Retrieves the three sync properties from each pre-acquired <see cref="StorageFile"/>, summing how many were
    /// returned so the call is not optimized away. The storage items are fixed; only the property store work varies.
    /// </summary>
    [Benchmark]
    public async Task<int> RetrieveProperties()
    {
        var total = 0;

        foreach (var file in _files)
        {
            var values = await file.Properties.RetrievePropertiesAsync(SyncProperties).AsTask().ConfigureAwait(false);
            total += values.Count;
        }

        return total;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _files = [];
        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private string[] CreateFiles(string groupName, int count)
    {
        var directory = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(directory);

        var paths = new string[count];
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(directory, $"file-{i:D6}.bin");
            File.WriteAllText(path, $"content-{i}");
            paths[i] = path;
        }

        return paths;
    }
}
