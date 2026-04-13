using TUnit.Core;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class VolumeInteropTests
{
    [Test]
    public async Task Test_GetVolumes_ReturnsAtLeastOneVolume()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volumes = sut.GetVolumes();

        // Assert
        await Assert.That(volumes.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Test_GetVolumes_ContainsNtfsVolume()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volumes = sut.GetVolumes();

        // Assert
        await Assert.That(volumes.Any(v =>
            string.Equals(v.FileSystemName, "NTFS", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task Test_GetVolumeForPath_ReturnsVolumeForSystemDrive()
    {
        // Arrange
        var sut = new VolumeInterop();
        var systemDrive = Environment.GetFolderPath(Environment.SpecialFolder.System);

        // Act
        var volume = sut.GetVolumeForPath(systemDrive);

        // Assert
        await Assert.That(volume).IsNotNull();
        await Assert.That(volume!.FileSystemName).IsNotNull();
    }

    [Test]
    public async Task Test_GetVolumeForPath_ReturnsNullForInvalidPath()
    {
        // Arrange
        var sut = new VolumeInterop();

        // Act
        var volume = sut.GetVolumeForPath(@"Z:\NonExistentDrive\Path");

        // Assert
        await Assert.That(volume).IsNull();
    }
}
