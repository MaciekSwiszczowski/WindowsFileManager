using WinUiFileManager.Application.Caching;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ThumbnailConversionCacheTests
{
    [Fact]
    public void GetOrConvert_Miss_InvokesFactoryAndReturnsImage()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();
        var calls = 0;

        var image = cache.GetOrConvert(Content(1), () =>
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

        var first = cache.GetOrConvert(Content(7), () => { calls++; return new FakeImage(); });
        var second = cache.GetOrConvert(Content(7), () => { calls++; return new FakeImage(); });

        Assert.Same(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrConvert_DifferentContent_CreatesSeparateEntries()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>();

        var first = cache.GetOrConvert(Content(1), static () => new FakeImage());
        var second = cache.GetOrConvert(Content(2), static () => new FakeImage());

        Assert.NotSame(first, second);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrConvert_OverCapacity_EvictsAndDisposesLeastRecentlyUsed()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>(capacity: 2);

        var a = cache.GetOrConvert(Content(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Content(2), static () => new FakeImage());
        var c = cache.GetOrConvert(Content(3), static () => new FakeImage());

        Assert.True(a.IsDisposed);
        Assert.False(b.IsDisposed);
        Assert.False(c.IsDisposed);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrConvert_Access_RefreshesRecencySoTheUsedEntrySurvives()
    {
        using var cache = new ThumbnailConversionCache<FakeImage>(capacity: 2);

        var a = cache.GetOrConvert(Content(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Content(2), static () => new FakeImage());
        var aAgain = cache.GetOrConvert(Content(1), static () => new FakeImage());
        var c = cache.GetOrConvert(Content(3), static () => new FakeImage());

        Assert.Same(a, aAgain);
        Assert.False(a.IsDisposed);
        Assert.True(b.IsDisposed);
        Assert.False(c.IsDisposed);
    }

    [Fact]
    public void Dispose_DisposesAllCachedImages()
    {
        var cache = new ThumbnailConversionCache<FakeImage>();
        var a = cache.GetOrConvert(Content(1), static () => new FakeImage());
        var b = cache.GetOrConvert(Content(2), static () => new FakeImage());

        cache.Dispose();

        Assert.True(a.IsDisposed);
        Assert.True(b.IsDisposed);
    }

    [Fact]
    public void GetOrConvert_AfterDispose_Throws()
    {
        var cache = new ThumbnailConversionCache<FakeImage>();
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.GetOrConvert(Content(1), static () => new FakeImage()));
    }

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
