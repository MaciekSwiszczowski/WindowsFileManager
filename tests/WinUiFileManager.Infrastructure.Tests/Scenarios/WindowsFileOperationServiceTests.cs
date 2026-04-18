using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.Execution;
using WinUiFileManager.Infrastructure.Planning;
using WinUiFileManager.Infrastructure.Tests.Fixtures;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsFileOperationServiceTests
{
    [Test]
    public async Task Test_CopySingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("source.txt", sizeInBytes: 256);
        var destDir = fixture.CreateDirectory("dest");
        var sut = CreateService();
        var plan = BuildCopyPlan(sourcePath, destDir);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsEqualTo(1);
        await Assert.That(File.Exists(Path.Combine(destDir, "source.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyMultipleFiles_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var source1 = fixture.CreateFile("a.txt", sizeInBytes: 100);
        var source2 = fixture.CreateFile("b.txt", sizeInBytes: 200);
        var destDir = fixture.CreateDirectory("dest");
        var sut = CreateService();
        var plan = BuildCopyPlan([source1, source2], destDir);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsEqualTo(2);
        await Assert.That(File.Exists(Path.Combine(destDir, "a.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(destDir, "b.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyToExistingDestination_ReportsError()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("conflict.txt", sizeInBytes: 100);
        var destDir = fixture.CreateDirectory("dest");
        fixture.CreateFile(@"dest\conflict.txt", sizeInBytes: 50);
        var sut = CreateService();
        var plan = BuildCopyPlan(sourcePath, destDir);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.FailedCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(summary.Status).IsNotEqualTo(OperationStatus.Succeeded);
    }

    [Test]
    public async Task Test_DeleteSingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("todelete.txt");
        var sut = CreateService();
        var plan = BuildDeletePlan(filePath, ItemKind.File);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task Test_DeleteDirectory_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var dirPath = fixture.CreateDirectory("emptydir");
        var sut = CreateService();
        var plan = BuildDeletePlan(dirPath, ItemKind.Directory);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(dirPath)).IsFalse();
    }

    [Test]
    public async Task Test_DeleteLockedFile_ReportsError()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("locked.txt", sizeInBytes: 64);
        using var lockHandle = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var sut = CreateService();
        var plan = BuildDeletePlan(filePath, ItemKind.File);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.FailedCount).IsEqualTo(1);
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Failed);
    }

    [Test]
    public async Task Test_CreateFolder_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var newFolderPath = Path.Combine(fixture.RootPath, "newfolder");
        var sut = CreateService();
        var plan = BuildCreateFolderPlan(newFolderPath);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(newFolderPath)).IsTrue();
    }

    [Test]
    public async Task Test_CreateFolder_AlreadyExists_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var existingDir = fixture.CreateDirectory("existing");
        var sut = CreateService();
        var plan = BuildCreateFolderPlan(existingDir);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(Directory.Exists(existingDir)).IsTrue();
    }

    [Test]
    public async Task Test_MoveSingleFile_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("moveme.txt", sizeInBytes: 128);
        var destDir = fixture.CreateDirectory("movedest");
        var destPath = Path.Combine(destDir, "moveme.txt");
        var sut = CreateService();
        var plan = BuildMovePlan(sourcePath, destPath);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(File.Exists(sourcePath)).IsFalse();
        await Assert.That(File.Exists(destPath)).IsTrue();
    }

    [Test]
    public async Task Test_ParallelCopy_Succeeds()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sources = Enumerable.Range(1, 10)
            .Select(i => fixture.CreateFile($"par_{i}.txt", sizeInBytes: 64))
            .ToList();
        var destDir = fixture.CreateDirectory("pardest");
        var sut = CreateService();
        var parallelOptions = new ParallelExecutionOptions(true, 4);
        var plan = BuildCopyPlan(sources, destDir, parallelOptions);

        // Act
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        // Assert
        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(summary.SucceededCount).IsEqualTo(10);
        foreach (var source in sources)
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(source));
            await Assert.That(File.Exists(destFile)).IsTrue();
        }
    }

    [Test]
    public async Task Test_CopyDirectoryStructure_DeepDestination_Succeeds()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceRoot = fixture.CreateDirectory("tree");
        var deepFile = fixture.CreateFile("tree/l1/l2/l3/note.txt", sizeInBytes: 20);
        var l1 = Path.Combine(sourceRoot, "l1");
        var l2 = Path.Combine(l1, "l2");
        var l3 = Path.Combine(l2, "l3");
        var destDir = fixture.CreateDirectory("out");
        var destRoot = Path.Combine(destDir, "tree");
        var destL1 = Path.Combine(destRoot, "l1");
        var destL2 = Path.Combine(destL1, "l2");
        var destL3 = Path.Combine(destL2, "l3");
        var destFile = Path.Combine(destL3, "note.txt");

        var items = new List<OperationItemPlan>
        {
            new(
                NormalizedPath.FromUserInput(sourceRoot),
                NormalizedPath.FromUserInput(destRoot),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(l1),
                NormalizedPath.FromUserInput(destL1),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(l2),
                NormalizedPath.FromUserInput(destL2),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(l3),
                NormalizedPath.FromUserInput(destL3),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(deepFile),
                NormalizedPath.FromUserInput(destFile),
                ItemKind.File,
                new FileInfo(deepFile).Length)
        };

        var plan = new OperationPlan(
            OperationType.Copy,
            items,
            NormalizedPath.FromUserInput(destDir),
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());

        var sut = CreateService();
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(File.Exists(destFile)).IsTrue();
        await Assert.That(File.Exists(deepFile)).IsTrue();
    }

    [Test]
    public async Task Test_ParallelCopyDirectoryStructure_DeepDestination_Succeeds()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourceRoot = fixture.CreateDirectory("tree");
        var fileA = fixture.CreateFile("tree/a/x/f.txt", sizeInBytes: 8);
        var fileB = fixture.CreateFile("tree/b/y/g.txt", sizeInBytes: 8);
        var fileC = fixture.CreateFile("tree/c/z/w/file3.txt", sizeInBytes: 8);
        var destDir = fixture.CreateDirectory("out");
        var destRoot = Path.Combine(destDir, "tree");
        var sa = Path.Combine(sourceRoot, "a");
        var sax = Path.Combine(sa, "x");
        var sb = Path.Combine(sourceRoot, "b");
        var sby = Path.Combine(sb, "y");
        var sc = Path.Combine(sourceRoot, "c");
        var scz = Path.Combine(sc, "z");
        var sczw = Path.Combine(scz, "w");
        var da = Path.Combine(destRoot, "a");
        var dax = Path.Combine(da, "x");
        var db = Path.Combine(destRoot, "b");
        var dby = Path.Combine(db, "y");
        var dc = Path.Combine(destRoot, "c");
        var dcz = Path.Combine(dc, "z");
        var dczw = Path.Combine(dcz, "w");

        var items = new List<OperationItemPlan>
        {
            new(
                NormalizedPath.FromUserInput(sourceRoot),
                NormalizedPath.FromUserInput(destRoot),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sa),
                NormalizedPath.FromUserInput(da),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sax),
                NormalizedPath.FromUserInput(dax),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sb),
                NormalizedPath.FromUserInput(db),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sby),
                NormalizedPath.FromUserInput(dby),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sc),
                NormalizedPath.FromUserInput(dc),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(scz),
                NormalizedPath.FromUserInput(dcz),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(sczw),
                NormalizedPath.FromUserInput(dczw),
                ItemKind.Directory,
                0),
            new(
                NormalizedPath.FromUserInput(fileA),
                NormalizedPath.FromUserInput(Path.Combine(dax, "f.txt")),
                ItemKind.File,
                new FileInfo(fileA).Length),
            new(
                NormalizedPath.FromUserInput(fileB),
                NormalizedPath.FromUserInput(Path.Combine(dby, "g.txt")),
                ItemKind.File,
                new FileInfo(fileB).Length),
            new(
                NormalizedPath.FromUserInput(fileC),
                NormalizedPath.FromUserInput(Path.Combine(dczw, "file3.txt")),
                ItemKind.File,
                new FileInfo(fileC).Length)
        };

        var parallelOptions = new ParallelExecutionOptions(true, 4);
        var plan = new OperationPlan(
            OperationType.Copy,
            items,
            NormalizedPath.FromUserInput(destDir),
            CollisionPolicy.Ask,
            parallelOptions);

        var sut = CreateService();
        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Succeeded);
        await Assert.That(File.Exists(Path.Combine(dax, "f.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dby, "g.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dczw, "file3.txt"))).IsTrue();
    }

    [Test]
    public async Task Test_CopyRejected_WhenDestinationRootFolderDoesNotExist()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var sourcePath = fixture.CreateFile("doc.txt", sizeInBytes: 16);
        var missingDestDir = Path.Combine(fixture.RootPath, "missing_target");
        var sut = CreateService();
        var plan = BuildCopyPlan(sourcePath, missingDestDir);

        var summary = await sut.ExecuteAsync(plan, null, CancellationToken.None);

        await Assert.That(summary.Status).IsEqualTo(OperationStatus.Failed);
        await Assert.That(summary.SucceededCount).IsEqualTo(0);
        await Assert.That(summary.Message).IsNotNull();
        await Assert.That(File.Exists(sourcePath)).IsTrue();
    }

    private static WindowsFileOperationService CreateService()
    {
        return new WindowsFileOperationService(
            new FileOperationInterop(),
            NullLogger<WindowsFileOperationService>.Instance);
    }

    private static OperationPlan BuildCopyPlan(string sourcePath, string destDir,
        ParallelExecutionOptions? parallelOptions = null)
    {
        return BuildCopyPlan([sourcePath], destDir, parallelOptions);
    }

    private static OperationPlan BuildCopyPlan(IReadOnlyList<string> sourcePaths, string destDir,
        ParallelExecutionOptions? parallelOptions = null)
    {
        var items = sourcePaths.Select(s => new OperationItemPlan(
            NormalizedPath.FromUserInput(s),
            NormalizedPath.FromUserInput(Path.Combine(destDir, Path.GetFileName(s))),
            ItemKind.File,
            new FileInfo(s).Length)).ToList();

        return new OperationPlan(
            OperationType.Copy,
            items,
            NormalizedPath.FromUserInput(destDir),
            CollisionPolicy.Ask,
            parallelOptions ?? new ParallelExecutionOptions());
    }

    private static OperationPlan BuildDeletePlan(string path, ItemKind kind)
    {
        var items = new List<OperationItemPlan>
        {
            new(NormalizedPath.FromUserInput(path), null, kind, 0)
        };

        return new OperationPlan(
            OperationType.Delete,
            items,
            null,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());
    }

    private static OperationPlan BuildCreateFolderPlan(string path)
    {
        var items = new List<OperationItemPlan>
        {
            new(NormalizedPath.FromUserInput(path), null, ItemKind.Directory, 0)
        };

        return new OperationPlan(
            OperationType.CreateFolder,
            items,
            null,
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());
    }

    private static OperationPlan BuildMovePlan(string source, string dest)
    {
        var destParent = Path.GetDirectoryName(dest)!;
        var items = new List<OperationItemPlan>
        {
            new(NormalizedPath.FromUserInput(source),
                NormalizedPath.FromUserInput(dest),
                ItemKind.File,
                new FileInfo(source).Length)
        };

        return new OperationPlan(
            OperationType.Move,
            items,
            NormalizedPath.FromUserInput(destParent),
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());
    }
}
