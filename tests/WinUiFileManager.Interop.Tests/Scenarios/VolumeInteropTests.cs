using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class VolumeInteropTests
{
    [Fact]
    public void GetVolumes_ReturnsAtLeastOneVolume()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volumes = sut.GetVolumes();

        // Assert
        Assert.True(volumes.Count >= 1);
    }

    [Fact]
    public void GetVolumes_ContainsNtfsVolume()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volumes = sut.GetVolumes();

        // Assert
        Assert.Contains(
            volumes,
            v => string.Equals(v.FileSystemName, "NTFS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetVolumeForPath_ReturnsVolumeForSystemDrive()
    {
        // Arrange
        var sut = new VolumeInterop();
        var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System);

        // Act
        var volume = sut.GetVolumeForPath(systemDrive);

        // Assert
        Assert.NotNull(volume);
        Assert.NotNull(volume!.FileSystemName);
    }

    [Fact]
    public void GetVolumeForPath_ReturnsNullForInvalidPath()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volume = sut.GetVolumeForPath(@"Z:\NonExistentDrive\Path");

        // Assert
        Assert.Null(volume);
    }
}
