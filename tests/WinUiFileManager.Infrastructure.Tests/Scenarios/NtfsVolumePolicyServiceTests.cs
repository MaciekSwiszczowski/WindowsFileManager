using TUnit.Core;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class NtfsVolumePolicyServiceTests
{
    [Test]
    public async Task Test_GetNtfsVolumes_ReturnsAtLeastOneVolume()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var volumes = await sut.GetNtfsVolumesAsync(CancellationToken.None);

        // Assert
        await Assert.That(volumes.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Test_IsNtfsPath_ReturnsTrueForSystemDrive()
    {
        // Arrange
        var sut = CreateService();
        var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System);

        // Act
        var isNtfs = await sut.IsNtfsPathAsync(systemDrive, CancellationToken.None);

        // Assert
        await Assert.That(isNtfs).IsTrue();
    }

    [Test]
    public async Task Test_ValidateNtfsPath_ReturnsTrueForValidPath()
    {
        // Arrange
        var sut = CreateService();
        var tempPath = Path.GetTempPath();

        // Act
        var result = sut.ValidateNtfsPath(tempPath);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Test_ValidateNtfsPath_ReturnsFalseForInvalidPath()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var result = sut.ValidateNtfsPath(string.Empty);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    private static NtfsVolumePolicyService CreateService()
    {
        return new NtfsVolumePolicyService(new VolumeInterop());
    }
}
