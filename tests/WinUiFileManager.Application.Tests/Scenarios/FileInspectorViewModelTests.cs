using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Tests.Fakes;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileInspectorViewModelTests
{
    [Test]
    public async Task Test_ApplySelection_ShowsBasicFieldsImmediately()
    {
        var sut = CreateSubject(new RecordingFileIdentityService());
        var entry = CreateEntry(
            name: "notes.txt",
            fullPath: @"C:\temp\notes.txt",
            kind: ItemKind.File,
            size: 4096);

        sut.ApplySelection(FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0));

        await Assert.That(sut.HasItem).IsTrue();
        await Assert.That(sut.IsLoadingDetails).IsTrue();
        await Assert.That(GetFieldValue(sut, "Basic", "Name")).IsEqualTo("notes.txt");
        await Assert.That(GetFieldValue(sut, "Basic", "Type")).IsEqualTo("File");
        await Assert.That(sut.Categories.Any(static category => category.Name == "Identity")).IsFalse();
        await Assert.That(sut.Categories.Any(static category => category.Name == "Locks")).IsFalse();
    }

    [Test]
    public async Task Test_LoadDeferredBatchesAsync_LoadsRequestedDeferredFields()
    {
        var identityService = new RecordingFileIdentityService();
        var sut = CreateSubject(identityService);
        var entry = CreateEntry(
            name: "beta.txt",
            fullPath: @"C:\temp\beta.txt",
            kind: ItemKind.File,
            size: 2048);

        var selection = FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0);
        sut.ApplySelection(selection);

        var batches = new List<FileInspectorDeferredBatchResult>();
        await foreach (var batch in sut.LoadDeferredBatchesAsync(selection, CancellationToken.None))
        {
            batches.Add(batch);
            sut.ApplyDeferredBatch(batch);
        }

        await Assert.That(batches.Count).IsEqualTo(2);
        await Assert.That(identityService.FileIdRequests.Count).IsEqualTo(1);
        await Assert.That(identityService.FileIdRequests[0]).IsEqualTo(entry.Model.FullPath.DisplayPath);
        await Assert.That(identityService.LockRequests.Count).IsEqualTo(1);
        await Assert.That(identityService.LockRequests[0]).IsEqualTo(entry.Model.FullPath.DisplayPath);
        await Assert.That(GetFieldValue(sut, "Identity", "NTFS File/Folder ID")).IsEqualTo("020304");
        await Assert.That(GetFieldValue(sut, "Locks", "Is locked")).IsEqualTo("True");
        await Assert.That(GetFieldValue(sut, "Locks", "In Use")).IsEqualTo("Yes");
        await Assert.That(sut.IsLoadingDetails).IsFalse();
    }

    [Test]
    public async Task Test_SearchText_FiltersByKeyAndValue()
    {
        var sut = CreateSubject(new RecordingFileIdentityService());
        var entry = CreateEntry(
            name: "docs",
            fullPath: @"C:\temp\docs",
            kind: ItemKind.Directory,
            size: -1);

        sut.ApplySelection(FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0));

        sut.SearchText = "folder";

        await Assert.That(sut.Categories.Count).IsEqualTo(1);
        await Assert.That(sut.Categories[0].Name).IsEqualTo("Basic");
        await Assert.That(sut.Categories[0].Fields.Count).IsEqualTo(1);
        await Assert.That(sut.Categories[0].Fields[0].Key).IsEqualTo("Type");

        sut.SearchText = "path";

        await Assert.That(sut.Categories.Count).IsEqualTo(1);
        await Assert.That(sut.Categories[0].Fields.Count).IsEqualTo(1);
        await Assert.That(sut.Categories[0].Fields[0].Key).IsEqualTo("Full Path");
    }

    [Test]
    public async Task Test_LoadDeferredBatchesAsync_ShowsIsLockedFalse_WhenItemIsNotLocked()
    {
        var sut = CreateSubject(new UnlockedFileIdentityService());
        var entry = CreateEntry(
            name: "report.docx",
            fullPath: @"C:\temp\report.docx",
            kind: ItemKind.File,
            size: 1024);

        var selection = FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0);
        sut.ApplySelection(selection);

        await foreach (var batch in sut.LoadDeferredBatchesAsync(selection, CancellationToken.None))
        {
            sut.ApplyDeferredBatch(batch);
        }

        await Assert.That(GetFieldValue(sut, "Locks", "Is locked")).IsEqualTo("False");
        await Assert.That(sut.Categories.Any(static category => category.Name == "Locks")).IsTrue();
        await Assert.That(sut.Categories.Single(static category => category.Name == "Locks").Fields.Count).IsEqualTo(1);
    }

    private static FileInspectorViewModel CreateSubject(IFileIdentityService identityService)
    {
        return new FileInspectorViewModel(
            identityService,
            new FakeClipboardService(),
            NullLogger<FileInspectorViewModel>.Instance);
    }

    private static FileEntryViewModel CreateEntry(string name, string fullPath, ItemKind kind, long size)
    {
        var normalizedPath = NormalizedPath.FromUserInput(fullPath);
        var model = new FileSystemEntryModel(
            normalizedPath,
            name,
            Path.GetExtension(name),
            kind,
            size,
            DateTime.UtcNow,
            DateTime.UtcNow,
            FileAttributes.Archive,
            NtfsFileId.None);

        return new FileEntryViewModel(model);
    }

    private static string GetFieldValue(FileInspectorViewModel sut, string category, string key)
    {
        return sut.Categories
            .Single(c => c.Name == category)
            .Fields
            .Single(f => f.Key == key)
            .Value;
    }

    private sealed class RecordingFileIdentityService : IFileIdentityService
    {
        public List<string> FileIdRequests { get; } = [];
        public List<string> LockRequests { get; } = [];

        public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
        {
            FileIdRequests.Add(path);
            return Task.FromResult(path.Contains("beta", StringComparison.OrdinalIgnoreCase)
                ? new NtfsFileId([0x02, 0x03, 0x04])
                : new NtfsFileId([0x01, 0x02, 0x03]));
        }

        public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            LockRequests.Add(path);
            return Task.FromResult(new FileLockDiagnostics(
                inUse: true,
                lockBy: [$"Locker:{Path.GetFileName(path)}"],
                lockPids: [1234],
                lockServices: ["SvcTest"],
                usage: "TestUsage",
                canSwitchTo: true,
                canClose: false));
        }
    }

    private sealed class UnlockedFileIdentityService : IFileIdentityService
    {
        public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NtfsFileId([0x01, 0x02, 0x03]));
        }

        public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLockDiagnostics(
                inUse: false,
                lockBy: [],
                lockPids: [],
                lockServices: [],
                usage: null,
                canSwitchTo: null,
                canClose: null));
        }
    }
}
