using WinUiFileManager.Application.Caching;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ThumbnailContentHashTests
{
    [Fact]
    public void Compute_SameContentSameDimensions_AreEqual()
    {
        var a = ThumbnailContentHash.Compute(Pixels(1), 4, 4);
        var b = ThumbnailContentHash.Compute(Pixels(1), 4, 4);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Compute_ByteIdenticalBufferDifferentDimensions_AreNotEqual()
    {
        var pixels = Pixels(7);

        var wide = ThumbnailContentHash.Compute(pixels, 4, 1);
        var tall = ThumbnailContentHash.Compute(pixels, 1, 4);

        Assert.NotEqual(wide, tall);
    }

    [Fact]
    public void Compute_DifferentContent_AreNotEqual()
    {
        var a = ThumbnailContentHash.Compute(Pixels(1), 4, 4);
        var b = ThumbnailContentHash.Compute(Pixels(2), 4, 4);

        Assert.NotEqual(a, b);
    }

    private static byte[] Pixels(byte value)
    {
        var bytes = new byte[16];
        Array.Fill(bytes, value);
        return bytes;
    }
}
