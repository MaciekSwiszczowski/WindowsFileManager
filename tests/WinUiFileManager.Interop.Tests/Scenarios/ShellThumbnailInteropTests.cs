using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class ShellThumbnailInteropTests
{
    [Fact]
    public void TryGetThumbnail_ForFolder_ReturnsTightlyPackedBgraPixels()
    {
        // Arrange
        var interop = new ShellThumbnailInterop();
        var directory = Path.Combine(Path.GetTempPath(), "WinUiFileManager_Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            // Act
            var produced = interop.TryGetThumbnail(directory, 48, out var thumbnail);

            // Assert
            Assert.True(produced);
            Assert.True(thumbnail.Width > 0);
            Assert.True(thumbnail.Height > 0);
            Assert.Equal(thumbnail.Width * 4, thumbnail.Stride);
            Assert.Equal(thumbnail.Height * thumbnail.Stride, thumbnail.Pixels.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryGetThumbnail_ForMissingPath_ReturnsFalse()
    {
        // Arrange
        var interop = new ShellThumbnailInterop();
        var missing = Path.Combine(Path.GetTempPath(), "WinUiFileManager_Tests", Guid.NewGuid().ToString("N"), "nope.txt");

        // Act
        var produced = interop.TryGetThumbnail(missing, 48, out var thumbnail);

        // Assert
        Assert.False(produced);
        Assert.Null(thumbnail.Pixels);
    }
}
