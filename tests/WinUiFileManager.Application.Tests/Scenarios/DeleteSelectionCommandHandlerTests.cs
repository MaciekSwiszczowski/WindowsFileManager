namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class DeleteSelectionCommandHandlerTests
{
    [Test]
    public async Task Test_DeleteSingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("remove_me.txt", sizeInBytes: 64);
        var sut = CreateHandler();
        var entries = await GetEntries(filePath);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task Test_DeleteMultipleFiles_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var path1 = fixture.CreateFile("del_a.txt");
        var path2 = fixture.CreateFile("del_b.txt");
        var sut = CreateHandler();
        var entries = await GetEntries(path1, path2);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(File.Exists(path1)).IsFalse();
        await Assert.That(File.Exists(path2)).IsFalse();
    }

    [Test]
    public async Task Test_DeleteDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var dirPath = fixture.CreateDirectory("stuff");
        fixture.CreateFile("stuff/a.txt", sizeInBytes: 32);
        fixture.CreateFile("stuff/b.txt", sizeInBytes: 64);
        var sut = CreateHandler();
        var entries = await GetEntries(dirPath);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(dirPath)).IsFalse();
    }

    private static DeleteSelectionCommandHandler CreateHandler()
    {
        var fileOpInterop = new FileOperationInterop();
        var planner = new WindowsFileOperationPlanner(NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        return new DeleteSelectionCommandHandler(
            planner,
            operationService,
            NullLogger<DeleteSelectionCommandHandler>.Instance);
    }

    private static async Task<IReadOnlyList<FileSystemEntryModel>> GetEntries(params string[] paths)
    {
        var pathService = new WindowsPathNormalizationService();
        var fileIdInterop = new FileIdentityInterop();
        var fsService = new WindowsFileSystemService(
            pathService, fileIdInterop, NullLogger<WindowsFileSystemService>.Instance);

        var entries = new List<FileSystemEntryModel>();
        foreach (var path in paths)
        {
            var entry = await fsService.GetEntryAsync(
                NormalizedPath.FromUserInput(path), CancellationToken.None);
            if (entry is not null)
                entries.Add(entry);
        }

        return entries;
    }
}
