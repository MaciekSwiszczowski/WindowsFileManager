using WinUiFileManager.Infrastructure.Services;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsPathNormalizationServiceTests
{
    [Fact]
    public void Normalize_ReturnsNormalizedPath()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();
        var rawPath = @"C:\Users\Test";

        // Act
        var result = sut.Normalize(rawPath);

        // Assert
        Assert.Equal(@"C:\Users\Test", result.DisplayPath);
        Assert.Equal(@"\\?\C:\Users\Test", result.Value);
    }

    [Fact]
    public void Validate_ValidPath_ReturnsValid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();

        // Act
        var result = sut.Validate(@"C:\Windows\System32");

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyPath_ReturnsInvalid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();

        // Act
        var result = sut.Validate(string.Empty);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Validate_InvalidChars_ReturnsInvalid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();
        var pathWithInvalidChars = "C:\\Invalid\0Path";

        // Act
        var result = sut.Validate(pathWithInvalidChars);

        // Assert
        Assert.False(result.IsValid);
    }
}
