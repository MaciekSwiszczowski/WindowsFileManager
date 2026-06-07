using Windows.Storage;

namespace WinUiFileManager.Benchmarks.StorageItemDisposal;

/// <summary>
/// Measures the steady-state native memory accumulation when <see cref="StorageFile"/> and
/// <see cref="StorageFolder"/> objects are acquired and dropped without any GC intervention,
/// mirroring what happens in the app during rapid file inspection.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="StorageItemDisposalBenchmarks"/>, this class performs no forced collection between
/// iterations. The <c>NativeMemoryProfiler</c> native-byte delta therefore reflects the natural
/// accumulation rate under real workload conditions — the same profile seen when clicking through files
/// in the Inspector. Comparing this to <see cref="StorageItemDisposalBenchmarks"/> isolates how much
/// of the app's observed growth is a GC-frequency problem vs. a deeper native-cache retention issue.
/// </para>
/// <para>
/// <strong>Reading the results:</strong>
/// <list type="bullet">
///   <item>A native delta that grows roughly proportionally to <c>FileCount × iterations</c> confirms
///   that the accumulation is driven by unfinalized COM RCWs — the fix is more frequent GC or a move
///   to a Win32 thumbnail/property API with explicit COM lifetime control.</item>
///   <item>A native delta that grows faster than the RCW count would predict (i.e. is not recovered
///   even after a subsequent <see cref="StorageItemDisposalBenchmarks"/> forced-GC run) points to a
///   native cache (Shell thumbnail pipeline, property handler) that grows independently of managed
///   object lifetime.</item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
[NativeMemoryProfiler]
[BenchmarkCategory("WinRT", "StorageItem")]
// ReSharper disable once ClassCanBeSealed.Global
public class StorageItemAccumulationBenchmarks
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
            nameof(StorageItemAccumulationBenchmarks));

        BenchmarkDirectoryCleanup.ForceDelete(_benchmarkDirectory);
        Directory.CreateDirectory(_benchmarkDirectory);

        _filePaths = CreateFiles("files", FileCount);
        _directoryPaths = CreateDirectories("directories", FileCount);
    }

    /// <summary>
    /// Acquires a <see cref="StorageFile"/> for each file path and a <see cref="StorageFolder"/> for each
    /// directory path, reads the item name, and lets the reference drop — no forced collection between
    /// iterations. The growing <c>NativeMemoryProfiler</c> delta across the five measurement iterations
    /// is the expected signal; the rate of growth per file is the actionable number.
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
