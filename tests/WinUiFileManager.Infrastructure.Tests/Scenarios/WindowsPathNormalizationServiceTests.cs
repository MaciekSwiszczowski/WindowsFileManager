using TUnit.Core;
using WinUiFileManager.Infrastructure.Services;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsPathNormalizationServiceTests
{
    [Test]
    public async Task Test_Normalize_ReturnsNormalizedPath()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();
        var rawPath = @"C:\Users\Test";

        // Act
        var result = sut.Normalize(rawPath);

        // Assert
        await Assert.That(result.DisplayPath).IsEqualTo(@"C:\Users\Test");
        await Assert.That(result.Value).IsEqualTo(@"\\?\C:\Users\Test");
    }

    [Test]
    public async Task Test_Validate_ValidPath_ReturnsValid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();

        // Act
        var result = sut.Validate(@"C:\Windows\System32");

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Test_Validate_EmptyPath_ReturnsInvalid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();

        // Act
        var result = sut.Validate(string.Empty);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task Test_Validate_InvalidChars_ReturnsInvalid()
    {
        // Arrange
        var sut = new WindowsPathNormalizationService();
        var pathWithInvalidChars = "C:\\Invalid\0Path";

        // Act
        var result = sut.Validate(pathWithInvalidChars);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }
}
