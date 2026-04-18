namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class CopySelectionCommandHandlerTests
{
    [Test]
    public async Task Test_CopySingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("doc.txt", sizeInBytes: 512);
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
        await Assert.That(File.Exists(Path.Combine(destDir, "doc.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyMultipleFiles_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var path1 = fixture.CreateFile("first.txt", sizeInBytes: 100);
        var path2 = fixture.CreateFile("second.txt", sizeInBytes: 200);
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
        await Assert.That(File.Exists(Path.Combine(destDir, "first.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(destDir, "second.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("subdir");
        fixture.CreateFile("subdir/inner.txt", sizeInBytes: 64);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(sourceDir);
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
        var copiedDir = Path.Combine(destDir, "subdir");
        await Assert.That(Directory.Exists(copiedDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedDir, "inner.txt"))).IsTrue();
        await Assert.That(Directory.Exists(sourceDir)).IsTrue();
    }

    private static CopySelectionCommandHandler CreateHandler()
    {
        var fileOpInterop = new FileOperationInterop();
        var planner = new WindowsFileOperationPlanner(NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        return new CopySelectionCommandHandler(
            planner,
            operationService,
            NullLogger<CopySelectionCommandHandler>.Instance);
    }

    private static async Task<IReadOnlyList<FileSystemEntryModel>> GetEntries(params string[] paths)
    {
        var pathService = new WindowsPathNormalizationService();
        var fsService = new WindowsFileSystemService(
            pathService, NullLogger<WindowsFileSystemService>.Instance);

        var entries = new List<FileSystemEntryModel>();
        foreach (var path in paths)
        {
            var entry = await fsService.GetEntryAsync(
                NormalizedPath.FromUserInput(path), CancellationToken.None);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }
}
