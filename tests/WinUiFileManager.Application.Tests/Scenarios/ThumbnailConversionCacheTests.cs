using WinUiFileManager.Application.Caching;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ThumbnailConversionCacheTests
{
    [Fact]
    public void GetOrConvert_Miss_InvokesFactoryAndReturnsImage()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();
        var calls = 0;

        var image = cache.GetOrConvert(Key(1), () =>
        {
            calls++;
            return new FakeImage();
        });

        Assert.Equal(1, calls);
        Assert.NotNull(image);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrConvert_IdenticalContent_DedupesToSingleConversion()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();
        var calls = 0;

        var first = cache.GetOrConvert(Key(7), () => { calls++; return new FakeImage(); });
        var second = cache.GetOrConvert(Key(7), () => { calls++; return new FakeImage(); });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task GetOrConvertAsync_IdenticalContent_DedupesToSingleConversion()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();
        var calls = 0;

        var first = await cache.GetOrConvertAsync(Key(5), () => { calls++; return Task.FromResult(new FakeImage()); });
        var second = await cache.GetOrConvertAsync(Key(5), () => { calls++; return Task.FromResult(new FakeImage()); });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrConvert_DifferentContent_CreatesSeparateEntries()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();

        var first = cache.GetOrConvert(Key(1), static () => new FakeImage());
        var second = cache.GetOrConvert(Key(2), static () => new FakeImage());

        Assert.NotSame(first, second);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrConvert_OverCapacity_EvictsAndDisposesLeastRecentlyUsed()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>(capacity: 2);

        var a = cache.GetOrConvert(Key(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Key(2), static () => new FakeImage());
        var c = cache.GetOrConvert(Key(3), static () => new FakeImage());

        Assert.True(a.IsDisposed);
        Assert.False(b.IsDisposed);
        Assert.False(c.IsDisposed);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrConvert_Access_RefreshesRecencySoTheUsedEntrySurvives()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>(capacity: 2);

        var a = cache.GetOrConvert(Key(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Key(2), static () => new FakeImage());
        var aAgain = cache.GetOrConvert(Key(1), static () => new FakeImage());
        var c = cache.GetOrConvert(Key(3), static () => new FakeImage());

        Assert.Same(a, aAgain);
        Assert.False(a.IsDisposed);
        Assert.True(b.IsDisposed);
        Assert.False(c.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesAllCachedImages()
    {
        var cache = new ThumbnailConversionCache<FakeImage>();
        var a = cache.GetOrConvert(Key(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Key(2), static () => new FakeImage());

        cache.Dispose();

        Assert.True(a.IsDisposed);
        Assert.True(b.IsDisposed);
    }

    [Fact]
    public async Task GetOrConvertAsync_DisposedDuringConversion_OrphansImageAndReturnsNull()
    {
        var cache = new ThumbnailConversionCache<FakeImage>();
        var gate = new TaskCompletionSource();
        var image = new FakeImage();
        var pending = cache.GetOrConvertAsync(Key(1), async () =>
        {
            await gate.Task;
            return image;
        });

        cache.Dispose();
        gate.SetResult();
        var result = await pending;

        Assert.Null(result);
        Assert.True(image.IsDisposed);
    }

    private static ThumbnailContentHash Key(byte value) => ThumbnailContentHash.Compute(Content(value), 8, 8);

    private static byte[] Content(byte value)
    {
        var bytes = new byte[64];
        Array.Fill(bytes, value);
        return bytes;
    }

    private sealed class FakeImage : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
