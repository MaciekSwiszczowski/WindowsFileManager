using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using WinUiFileManager.Application.Caching;

namespace WinUiFileManager.Benchmarks.Caching;

/// <summary>
/// Measures what <see cref="ThumbnailConversionCache{TImage}"/> saves by skipping the bytes → image conversion when
/// the thumbnail bytes repeat. The conversion modelled here is <see cref="SoftwareBitmap.CreateCopyFromBuffer"/> —
/// the heavy WinRT step the inspector's UI hop performs before wrapping the result in a <c>SoftwareBitmapSource</c>.
/// </summary>
/// <remarks>
/// <strong>Reading the results:</strong> <c>Cached_IdenticalContent</c> converts once and serves the rest from the
/// cache, so its <c>Allocated</c> should be a fraction of <c>NoCache_ConvertEach</c> (the folder-of-one-type case).
/// <c>Cached_DistinctContent</c> never hits, so it shows the pure overhead the cache adds when every thumbnail is
/// unique (one hash per item, no conversion saved).
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("Thumbnail", "Cache")]
// ReSharper disable once ClassCanBeSealed.Global
public class ThumbnailConversionCacheBenchmarks
{
    private const int Edge = 48;

    [Params(20)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int FileCount { get; set; }

    private byte[] _identicalPixels = [];
    private byte[][] _distinctPixels = [];

    [GlobalSetup]
    public void Setup()
    {
        _identicalPixels = CreatePixels(0);
        _distinctPixels = new byte[FileCount][];
        for (var i = 0; i < FileCount; i++)
        {
            _distinctPixels[i] = CreatePixels((byte)(i + 1));
        }
    }

    /// <summary>Baseline: convert every thumbnail, with no cache.</summary>
    [Benchmark(Baseline = true)]
    public int NoCache_ConvertEach()
    {
        var total = 0;
        for (var i = 0; i < FileCount; i++)
        {
            using var bitmap = Convert(_identicalPixels);
            total += bitmap.PixelWidth;
        }

        return total;
    }

    /// <summary>All thumbnails share the same bytes: one conversion, the rest served from the cache.</summary>
    [Benchmark]
    public int Cached_IdenticalContent()
    {
        using var cache = new ThumbnailConversionCache<SoftwareBitmap>();
        var pixels = _identicalPixels;
        var factory = new Func<SoftwareBitmap>(() => Convert(pixels));

        var total = 0;
        for (var i = 0; i < FileCount; i++)
        {
            total += cache.GetOrConvert(pixels, factory).PixelWidth;
        }

        return total;
    }

    /// <summary>Every thumbnail is unique: pure cache overhead (one hash each, no conversion saved).</summary>
    [Benchmark]
    public int Cached_DistinctContent()
    {
        using var cache = new ThumbnailConversionCache<SoftwareBitmap>(capacity: FileCount);
        var total = 0;
        for (var i = 0; i < FileCount; i++)
        {
            var pixels = _distinctPixels[i];
            total += cache.GetOrConvert(pixels, () => Convert(pixels)).PixelWidth;
        }

        return total;
    }

    private static SoftwareBitmap Convert(byte[] pixels) =>
        SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            Edge,
            Edge,
            BitmapAlphaMode.Premultiplied);

    private static byte[] CreatePixels(byte seed)
    {
        var pixels = new byte[Edge * Edge * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(seed + i);
        }

        return pixels;
    }
}
