namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class RenameEntryCommandHandlerTests
{
    [Test]
    public async Task Test_RenameFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var oldPath = fixture.CreateFile("old.txt", sizeInBytes: 128);
        var sut = CreateHandler();
        var entries = await GetEntries(oldPath);
        var entry = entries[0];

        // Act
        var summary = await sut.ExecuteAsync(entry, "new.txt", CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        var parentDir = Path.GetDirectoryName(oldPath)!;
        await Assert.That(File.Exists(Path.Combine(parentDir, "new.txt"))).IsTrue();
        await Assert.That(File.Exists(oldPath)).IsFalse();
    }

    [Test]
    public async Task Test_RenameDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var oldDir = fixture.CreateDirectory("olddir");
        fixture.CreateFile("olddir/child.txt", sizeInBytes: 64);
        var sut = CreateHandler();
        var entries = await GetEntries(oldDir);
        var entry = entries[0];

        // Act
        var summary = await sut.ExecuteAsync(entry, "newdir", CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        var parentDir = Path.GetDirectoryName(oldDir)!;
        await Assert.That(Directory.Exists(Path.Combine(parentDir, "newdir"))).IsTrue();
        await Assert.That(Directory.Exists(oldDir)).IsFalse();
    }

    private static RenameEntryCommandHandler CreateHandler()
    {
        var fileOpInterop = new FileOperationInterop();
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        return new RenameEntryCommandHandler(
            operationService,
            NullLogger<RenameEntryCommandHandler>.Instance);
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
                entries.Add(entry);
        }

        return entries;
    }
}
