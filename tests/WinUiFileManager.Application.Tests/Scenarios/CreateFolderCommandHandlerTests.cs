namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class CreateFolderCommandHandlerTests
{
    [Test]
    public async Task Test_CreateFolder_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var parentDir = NormalizedPath.FromUserInput(fixture.RootPath);
        var sut = CreateHandler();

        // Act
        var summary = await sut.ExecuteAsync(
            parentDir,
            "NewFolder",
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(Path.Combine(fixture.RootPath, "NewFolder"))).IsTrue();
    }

    [Test]
    public async Task Test_CreateFolder_AlreadyExists_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateDirectory("ExistingFolder");
        var parentDir = NormalizedPath.FromUserInput(fixture.RootPath);
        var sut = CreateHandler();

        // Act
        var summary = await sut.ExecuteAsync(
            parentDir,
            "ExistingFolder",
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(Path.Combine(fixture.RootPath, "ExistingFolder"))).IsTrue();
    }

    private static CreateFolderCommandHandler CreateHandler()
    {
        var fileOpInterop = new FileOperationInterop();
        var planner = new WindowsFileOperationPlanner(NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        return new CreateFolderCommandHandler(
            planner,
            operationService,
            NullLogger<CreateFolderCommandHandler>.Instance);
    }
}
