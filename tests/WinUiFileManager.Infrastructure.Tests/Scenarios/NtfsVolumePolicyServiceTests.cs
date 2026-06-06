using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class NtfsVolumePolicyServiceTests
{
    [Fact]
    public async Task GetNtfsVolumes_ReturnsAtLeastOneVolume()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var volumes = await sut.GetNtfsVolumesAsync(CancellationToken.None);

        // Assert
        Assert.True(volumes.Count >= 1);
    }

    [Fact]
    public async Task IsNtfsPath_ReturnsTrueForSystemDrive()
    {
        // Arrange
        var sut = CreateService();
        var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System);

        // Act
        var isNtfs = await sut.IsNtfsPathAsync(systemDrive, CancellationToken.None);

        // Assert
        Assert.True(isNtfs);
    }

    [Fact]
    public void ValidateNtfsPath_ReturnsTrueForValidPath()
    {
        // Arrange
        var sut = CreateService();
        var tempPath = Path.GetTempPath();

        // Act
        var result = sut.ValidateNtfsPath(tempPath);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateNtfsPath_ReturnsFalseForInvalidPath()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var result = sut.ValidateNtfsPath(string.Empty);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    private static NtfsVolumePolicyService CreateService()
    {
        return new NtfsVolumePolicyService(new VolumeInterop());
    }
}
