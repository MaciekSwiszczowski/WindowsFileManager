using WinUiFileManager.Application.Navigation;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class GoToPathCommandHandlerTests
{
    [Test]
    public async Task Test_GoToPath_ValidNtfsPath_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateFile("readme.txt");
        fixture.CreateDirectory("docs");
        var sut = CreateHandler();

        // Act
        var result = await sut.ExecuteAsync(fixture.RootPath, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Path).IsNotNull();
        await Assert.That(result.Entries).IsNotNull();
        await Assert.That(result.Entries!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Test_GoToPath_NonExistentPath_ReturnsFailure()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var missingPath = Path.Combine(fixture.RootPath, "does_not_exist");
        var sut = CreateHandler();

        // Act
        var result = await sut.ExecuteAsync(missingPath, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    private static GoToPathCommandHandler CreateHandler()
    {
        var volumeInterop = new VolumeInterop();
        var pathService = new WindowsPathNormalizationService();
        var volumePolicy = new NtfsVolumePolicyService(volumeInterop);
        var fileSystemService = new WindowsFileSystemService(
            pathService, NullLogger<WindowsFileSystemService>.Instance);

        return new GoToPathCommandHandler(
            fileSystemService,
            volumePolicy,
            pathService,
            NullLogger<GoToPathCommandHandler>.Instance);
    }
}
