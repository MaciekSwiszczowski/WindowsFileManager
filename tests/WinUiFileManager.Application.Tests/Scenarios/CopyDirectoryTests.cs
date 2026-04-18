namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class CopyDirectoryTests
{
    [Test]
    public async Task Test_CopyDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("source_folder");
        fixture.CreateFile("source_folder/file1.txt", sizeInBytes: 100);
        fixture.CreateFile("source_folder/file2.txt", sizeInBytes: 200);
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
        var copiedDir = Path.Combine(destDir, "source_folder");
        await Assert.That(Directory.Exists(copiedDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedDir, "file1.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedDir, "file2.txt"))).IsTrue();
        await Assert.That(Directory.Exists(sourceDir)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(sourceDir, "file1.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(sourceDir, "file2.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyDirectory_WithDeeplyNestedFiles_Succeeds()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("deep_root");
        fixture.CreateFile("deep_root/level1/level2/level3/leaf.txt", sizeInBytes: 48);
        fixture.CreateFile("deep_root/level1/other.txt", sizeInBytes: 16);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(sourceDir);
        var destination = NormalizedPath.FromUserInput(destDir);

        var summary = await sut.ExecuteAsync(
            entries,
            destination,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions(),
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        var copiedRoot = Path.Combine(destDir, "deep_root");
        await Assert.That(File.Exists(Path.Combine(copiedRoot, "level1", "level2", "level3", "leaf.txt")))
            .IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedRoot, "level1", "other.txt"))).IsTrue();
        await Assert.That(Directory.Exists(sourceDir)).IsTrue();
    }

    [Test]
    public async Task Test_CopyDirectory_WithDeeplyNestedFiles_Parallel_Succeeds()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceDir = fixture.CreateDirectory("deep_root");
        fixture.CreateFile("deep_root/a/x/file1.txt", sizeInBytes: 32);
        fixture.CreateFile("deep_root/b/y/file2.txt", sizeInBytes: 32);
        fixture.CreateFile("deep_root/c/z/w/file3.txt", sizeInBytes: 32);
        var destDir = fixture.CreateDirectory("target");
        var sut = CreateHandler();
        var entries = await GetEntries(sourceDir);
        var destination = NormalizedPath.FromUserInput(destDir);
        var parallel = new ParallelExecutionOptions(Enabled: true, MaxDegreeOfParallelism: 4);

        var summary = await sut.ExecuteAsync(
            entries,
            destination,
            CollisionPolicy.Ask,
            parallel,
            new Progress<OperationProgressEvent>(),
            CancellationToken.None);

        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        var copiedRoot = Path.Combine(destDir, "deep_root");
        await Assert.That(File.Exists(Path.Combine(copiedRoot, "a", "x", "file1.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedRoot, "b", "y", "file2.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(copiedRoot, "c", "z", "w", "file3.txt"))).IsTrue();
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
