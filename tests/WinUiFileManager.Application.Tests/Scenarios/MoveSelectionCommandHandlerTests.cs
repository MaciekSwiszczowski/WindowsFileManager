namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class MoveSelectionCommandHandlerTests
{
    [Test]
    public async Task Test_MoveSingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("moveme.txt", sizeInBytes: 256);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(sourcePath);
        var destination = NormalizedPath.FromUserInput(destDir);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            destination,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions(),
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsEqualTo(1);
        await Assert.That(File.Exists(Path.Combine(destDir, "moveme.txt"))).IsTrue();
        await Assert.That(File.Exists(sourcePath)).IsFalse();
    }

    [Test]
    public async Task Test_MoveMultipleFiles_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var path1 = fixture.CreateFile("alpha.txt", sizeInBytes: 100);
        var path2 = fixture.CreateFile("beta.txt", sizeInBytes: 200);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(path1, path2);
        var destination = NormalizedPath.FromUserInput(destDir);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            destination,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions(),
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsEqualTo(2);
        await Assert.That(File.Exists(Path.Combine(destDir, "alpha.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(destDir, "beta.txt"))).IsTrue();
        await Assert.That(File.Exists(path1)).IsFalse();
        await Assert.That(File.Exists(path2)).IsFalse();
    }

    [Test]
    public async Task Test_MoveDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var dirPath = fixture.CreateDirectory("docs");
        fixture.CreateFile("docs/readme.txt", sizeInBytes: 64);
        fixture.CreateFile("docs/notes.txt", sizeInBytes: 128);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(dirPath);
        var destination = NormalizedPath.FromUserInput(destDir);

        // Act
        var summary = await sut.ExecuteAsync(
            entries,
            destination,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions(),
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        var movedDir = Path.Combine(destDir, "docs");
        await Assert.That(Directory.Exists(movedDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(movedDir, "readme.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(movedDir, "notes.txt"))).IsTrue();
        await Assert.That(Directory.Exists(dirPath)).IsFalse();
    }

    private static MoveSelectionCommandHandler CreateHandler()
    {
        var fileOpInterop = new FileOperationInterop();
        var planner = new WindowsFileOperationPlanner(NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        return new MoveSelectionCommandHandler(
            planner,
            operationService,
            NullLogger<MoveSelectionCommandHandler>.Instance);
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
