using Windows.Storage;

namespace WinUiFileManager.Benchmarks.StorageItemDisposal;

/// <summary>
/// Tests whether a full blocking Gen-2 collection plus finalizer drain is sufficient to reclaim the
/// native COM memory held by <see cref="StorageFile"/> and <see cref="StorageFolder"/> objects after use.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StorageFile"/> and <see cref="StorageFolder"/> do <em>not</em> implement
/// <see cref="IDisposable"/> in the current <c>net10.0-windows10.0.19041.0</c> CsWinRT projection
/// (a compile-time <c>CS0039</c> error confirms this). Native COM references are therefore released
/// exclusively by the GC finalizer thread, which runs non-deterministically under low managed-heap
/// pressure. <c>InspectorThumbnailDiagnosticsHandler</c> and <c>InspectorCloudDiagnosticsHandler</c>
/// both acquire these items on every file inspection without any explicit release path.
/// </para>
/// <para>
/// <strong>Reading the results:</strong> the <c>NativeMemoryProfiler</c> native-byte delta across
/// iterations is the primary signal. <see cref="IterationCleanup"/> forces a complete Gen-2 collection
/// and waits for all finalizers to drain before the next iteration begins.
/// <list type="bullet">
///   <item>If the native delta is near zero — GC finalization is successfully releasing the COM ref.
///   The app leak is a frequency problem: GC is not running fast enough to keep up with inspection
///   throughput. A periodic nudge or a migration to a Win32 thumbnail/property API is the remedy.</item>
///   <item>If the native delta remains positive after every forced collection — finalization alone
///   cannot release the COM ref, which points to an internal native cache (e.g. the Shell thumbnail
///   pipeline) holding the memory independently of the managed object lifetime.</item>
/// </list>
/// Compare these results with <see cref="StorageItemAccumulationBenchmarks"/>, which runs the same
/// acquisition loop without any forced collection to show natural steady-state accumulation.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("WinRT", "StorageItem")]
// ReSharper disable once ClassCanBeSealed.Global
public class StorageItemDisposalBenchmarks
{
    [Params(10, 50)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private string _benchmarkDirectory = string.Empty;
    private string[] _filePaths = [];
    private string[] _directoryPaths = [];

    [GlobalSetup]
    public void Setup()
    {
        _benchmarkDirectory = Path.Combine(
            BenchmarkProjectConfig.BenchmarkDirectory,
            nameof(StorageItemDisposalBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateFiles("files", FileCount);
        _directoryPaths = CreateDirectories("directories", FileCount);
    }

    /// <summary>
    /// Acquires a <see cref="StorageFile"/> for each file path and a <see cref="StorageFolder"/> for each
    /// directory path, reads the item name, and lets the reference drop. The <see cref="IterationCleanup"/>
    /// then drives a full Gen-2 collection plus finalizer drain. The <c>NativeMemoryProfiler</c> delta
    /// shows whether the forced collection reclaims the underlying COM memory each iteration.
    /// </summary>
    [Benchmark]
    public async Task<int> AcquireStorageItems()
    {
        var total = 0;

        foreach (var path in _filePaths)
        {
            var item = await StorageFile.GetFileFromPathAsync(path);
            total += item.Name.Length;
        }

        foreach (var path in _directoryPaths)
        {
            var item = await StorageFolder.GetFolderFromPathAsync(path);
            total += item.Name.Length;
        }

        return total;
    }

    /// <summary>
    /// Drives a full blocking Gen-2 collection and waits for all finalizers to drain before the
    /// next iteration. Finalizers on the dropped <see cref="StorageFile"/> / <see cref="StorageFolder"/>
    /// RCWs are the only mechanism that can release their native COM references; this cleanup gives
    /// them every opportunity to do so.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _filePaths = [];
        _directoryPaths = [];
        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
    }

    private string[] CreateFiles(string groupName, int count)
    {
        var dir = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(dir);

        var paths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(dir, $"file-{i:D6}.bin");
            File.WriteAllText(path, $"content-{i}");
            paths[i] = path;
        }

        return paths;
    }

    private string[] CreateDirectories(string groupName, int count)
    {
        var dir = Path.Combine(_benchmarkDirectory, groupName);
        Directory.CreateDirectory(dir);

        var paths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(dir, $"dir-{i:D6}");
            Directory.CreateDirectory(path);
            paths[i] = path;
        }

        return paths;
    }
}
