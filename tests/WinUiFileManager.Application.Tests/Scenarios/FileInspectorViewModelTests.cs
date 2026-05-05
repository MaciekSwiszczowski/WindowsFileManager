using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using Microsoft.UI.Xaml;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Tests.Fakes;
using WinUiFileManager.Application.Tests.Fixtures;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileInspectorViewModelTests
{
    [Test]
    public async Task Test_ApplySelection_ShowsBasicFieldsImmediately()
    {
        using var sut = CreateSubject(new RecordingFileIdentityService());
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
        await Assert.That(GetFieldValue(sut, "NTFS", "Archive")).IsEqualTo("Yes");
        await Assert.That(GetFieldValue(sut, "NTFS", "Read Only")).IsEqualTo("No");
        await Assert.That(GetVisibleCategories(sut).Any(static category => category.Name == "IDs")).IsFalse();
        await Assert.That(GetVisibleCategories(sut).Any(static category => category.Name == "Locks")).IsFalse();
    }

    [Test]
    public async Task Test_LoadDeferredBatchesAsync_LoadsRequestedDeferredFields()
    {
        var identityService = new RecordingFileIdentityService();
        using var sut = CreateSubject(identityService);
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

        await Assert.That(batches.Count).IsEqualTo(7);
        await Assert.That(identityService.FileIdRequests.Count).IsEqualTo(1);
        await Assert.That(identityService.FileIdRequests[0]).IsEqualTo(entry.Model?.FullPath.DisplayPath);
        await Assert.That(identityService.LockRequests.Count).IsEqualTo(1);
        await Assert.That(identityService.LockRequests[0]).IsEqualTo(entry.Model?.FullPath.DisplayPath);
        await Assert.That(GetFieldValue(sut, "NTFS", "Read Only")).IsEqualTo("No");
        await Assert.That(GetFieldValue(sut, "NTFS", "Accessed")).IsNotEqualTo(string.Empty);
        await Assert.That(GetFieldValue(sut, "NTFS", "MFT Changed")).IsNotEqualTo(string.Empty);
        await Assert.That(GetFieldValue(sut, "IDs", "File ID")).IsEqualTo("020304");
        await Assert.That(GetFieldValue(sut, "Locks", "Is locked")).IsEqualTo("True");
        await Assert.That(GetFieldValue(sut, "Locks", "In Use")).IsEqualTo("Yes");
        await Assert.That(GetFieldValue(sut, "Links", "Link Target")).IsEqualTo(string.Empty);
        await Assert.That(GetFieldValue(sut, "Streams", "Alternate Stream Count")).IsEqualTo("1");
        await Assert.That(GetFieldValue(sut, "Security", "Owner")).IsEqualTo("DOMAIN\\Owner");
        await Assert.That(GetFieldValue(sut, "Thumbnails", "Has Thumbnail")).IsEqualTo("Yes");
        await Assert.That(GetFieldValue(sut, "Cloud", "Status")).IsEqualTo("Pinned, Hydrated, Synced, Upload pending, Provider custom");
        await Assert.That(sut.IsLoadingDetails).IsFalse();
    }

    [Test]
    public async Task Test_ToggleNtfsFlagAsync_RequestsAttributeUpdateAndRefresh()
    {
        var identityService = new RecordingFileIdentityService();
        using var sut = CreateSubject(identityService);
        var entry = CreateEntry(
            name: "alpha.txt",
            fullPath: @"C:\temp\alpha.txt",
            kind: ItemKind.File,
            size: 128);

        sut.ApplySelection(FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0));

        var readOnlyField = sut.Fields.Single(static field => field.Key == "Read Only");
        await readOnlyField.ToggleCommand!.ExecuteAsync(null);

        await Assert.That(identityService.ToggleRequests.Count).IsEqualTo(1);
        await Assert.That(identityService.ToggleRequests[0]).IsEqualTo((@"C:\temp\alpha.txt", FileAttributes.ReadOnly, true));
    }

    [Test]
    public async Task Test_SearchText_FiltersByKeyAndValue()
    {
        using var sut = CreateSubject(new RecordingFileIdentityService());
        var entry = CreateEntry(
            name: "docs",
            fullPath: @"C:\temp\docs",
            kind: ItemKind.Directory,
            size: -1);

        sut.ApplySelection(FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0));

        sut.SearchText = "folder";

        var visibleCategories = GetVisibleCategories(sut);
        await Assert.That(visibleCategories.Count).IsEqualTo(1);
        await Assert.That(visibleCategories[0].Name).IsEqualTo("Basic");
        await Assert.That(GetVisibleFields(sut, visibleCategories[0]).Count).IsEqualTo(1);
        await Assert.That(GetVisibleFields(sut, visibleCategories[0])[0].Key).IsEqualTo("Type");

        sut.SearchText = "path";

        visibleCategories = GetVisibleCategories(sut);
        await Assert.That(visibleCategories.Count).IsEqualTo(1);
        await Assert.That(GetVisibleFields(sut, visibleCategories[0]).Count).IsEqualTo(1);
        await Assert.That(GetVisibleFields(sut, visibleCategories[0])[0].Key).IsEqualTo("Full Path");
    }

    [Test]
    public async Task Test_LoadDeferredBatchesAsync_ShowsIsLockedFalse_WhenItemIsNotLocked()
    {
        using var sut = CreateSubject(new UnlockedFileIdentityService());
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
        await Assert.That(GetVisibleCategories(sut).Any(static category => category.Name == "Locks")).IsTrue();
        await Assert.That(GetVisibleFields(
            sut,
            sut.Categories.Single(static category => category.Name == "Locks")).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Test_LoadDeferredBatchesAsync_PropagatesCancellationFromNtfsCloudBatch()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("cloud.txt", 128);
        var selection = FileInspectorSelection.FromSelection(
            [CreateEntry("cloud.txt", filePath, ItemKind.File, 128)],
            isPaneLoading: false,
            refreshVersion: 0);
        var cloudBatchEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCloudPropertyValuesProvider = NtfsFileIdentityService.CloudPropertyValuesProvider;
        using var sut = CreateSubject(new CloudBatchCancellationIdentityService());
        using var cancellationSource = new CancellationTokenSource();
        await using var enumerator = sut.LoadDeferredBatchesAsync(
            selection,
            cancellationSource.Token).GetAsyncEnumerator();

        try
        {
            NtfsFileIdentityService.CloudPropertyValuesProvider = async (_, cancellationToken) =>
            {
                cloudBatchEntered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return (string.Empty, string.Empty, string.Empty);
            };

            for (var i = 0; i < 6; i++)
            {
                await Assert.That(await enumerator.MoveNextAsync()).IsTrue();
            }

            var finalBatchTask = enumerator.MoveNextAsync().AsTask();
            await cloudBatchEntered.Task;
            await cancellationSource.CancelAsync();

            var canceled = false;
            try
            {
                await finalBatchTask;
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            await Assert.That(canceled).IsTrue();
        }
        finally
        {
            NtfsFileIdentityService.CloudPropertyValuesProvider = originalCloudPropertyValuesProvider;
        }
    }

    [Test]
    public async Task Test_ShowPropertiesCommand_UsesShellServiceForCurrentSelection()
    {
        var shellService = new FakeShellService();
        using var sut = new FileInspectorViewModel(
            new RecordingFileIdentityService(),
            new FakeClipboardService(),
            shellService,
            new FileTableFocusService(),
            new TestSchedulerProvider(new TestScheduler()),
            NullLogger<FileInspectorViewModel>.Instance);
        var entry = CreateEntry(
            name: "image.jpg",
            fullPath: @"C:\temp\image.jpg",
            kind: ItemKind.File,
            size: 128);

        sut.ApplySelection(FileInspectorSelection.FromSelection([entry], isPaneLoading: false, refreshVersion: 0));

        await sut.ShowPropertiesCommand.ExecuteAsync(null);

        await Assert.That(shellService.LastPropertiesPath?.DisplayPath).IsEqualTo(@"C:\temp\image.jpg");
    }

    private static FileInspectorViewModel CreateSubject(IFileIdentityService identityService)
    {
        return new FileInspectorViewModel(
            identityService,
            new FakeClipboardService(),
            new FakeShellService(),
            new FileTableFocusService(),
            new TestSchedulerProvider(new TestScheduler()),
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
            FileAttributes.Archive);

        return new FileEntryViewModel(model);
    }

    private static string GetFieldValue(FileInspectorViewModel sut, string category, string key)
    {
        return sut.Fields
            .Single(f =>
                string.Equals(f.Category, category, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static List<FileInspectorCategoryViewModel> GetVisibleCategories(FileInspectorViewModel sut)
    {
        return sut.Categories
            .Where(static category => category.Visibility == Visibility.Visible)
            .ToList();
    }

    private static List<FileInspectorFieldViewModel> GetVisibleFields(
        FileInspectorViewModel sut,
        FileInspectorCategoryViewModel category)
    {
        return sut.Fields
            .Where(field => field.IsVisible && string.Equals(field.Category, category.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static field => field.SortOrder)
            .ToList();
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

        public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
        {
            FileIdRequests.Add(path);
            return Task.FromResult(new FileIdentityDetails(
                path.Contains("beta", StringComparison.OrdinalIgnoreCase)
                    ? new NtfsFileId([0x02, 0x03, 0x04])
                    : new NtfsFileId([0x01, 0x02, 0x03]),
                "0000ABCD",
                "0x00000000000000FE",
                "1",
                Path.GetFullPath(path)));
        }

        public Task<FileNtfsMetadataDetails> GetNtfsMetadataDetailsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileNtfsMetadataDetails(
                FileAttributes.Archive,
                new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 10, 1, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 10, 2, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 10, 3, 0, DateTimeKind.Utc)));
        }

        public List<(string Path, FileAttributes Flag, bool Enabled)> ToggleRequests { get; } = [];

        public Task<bool> SetNtfsAttributeFlagAsync(string path, FileAttributes flag, bool enabled, CancellationToken cancellationToken)
        {
            ToggleRequests.Add((path, flag, enabled));
            return Task.FromResult(true);
        }

        public Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileCloudDiagnosticsDetails(
                true,
                "Pinned, Hydrated, Synced, Upload pending, Provider custom",
                "OneDrive",
                @"C:\Users\Lenovo\OneDrive",
                "SyncRoot-1",
                "00000000-0000-0000-0000-000000000001",
                "Yes",
                "Upload pending",
                "Provider custom"));
        }

        public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        }

        public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("1", ["Zone.Identifier:$DATA (24 bytes)"]));
        }

        public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(
                "DOMAIN\\Owner",
                "DOMAIN\\Group",
                "Allow 2, Deny 1, Inherited 1",
                "Success 0, Failure 0",
                true,
                false));
        }

        public Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            var thumbnailBytes = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2Zb9kAAAAASUVORK5CYII=");

            return Task.FromResult(new FileThumbnailDiagnosticsDetails(thumbnailBytes, Path.GetExtension(path)));
        }

        public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            LockRequests.Add(path);
            return Task.FromResult(new FileLockDiagnostics(
                inUse: true,
                lockBy: [$"Locker:{Path.GetFileName(path)}"],
                lockPids: [1234],
                lockServices: ["SvcTest"]));
        }
    }

    private sealed class UnlockedFileIdentityService : IFileIdentityService
    {
        public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NtfsFileId([0x01, 0x02, 0x03]));
        }

        public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileIdentityDetails(
                new NtfsFileId([0x01, 0x02, 0x03]),
                string.Empty,
                string.Empty,
                string.Empty,
                Path.GetFullPath(path)));
        }

        public Task<FileNtfsMetadataDetails> GetNtfsMetadataDetailsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileNtfsMetadataDetails(
                FileAttributes.Archive,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }

        public Task<bool> SetNtfsAttributeFlagAsync(string path, FileAttributes flag, bool enabled, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(FileCloudDiagnosticsDetails.None);
        }

        public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        }

        public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("0", []));
        }

        public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, null, null));
        }

        public Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileThumbnailDiagnosticsDetails(null, string.Empty));
        }

        public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLockDiagnostics(
                inUse: false,
                lockBy: [],
                lockPids: [],
                lockServices: []));
        }
    }

    private sealed class CloudBatchCancellationIdentityService : IFileIdentityService
    {
        private readonly NtfsFileIdentityService _service = new(new RestartManagerInterop(), new CloudFilesInterop());

        public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NtfsFileId([0x01, 0x02, 0x03]));
        }

        public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileIdentityDetails(
                new NtfsFileId([0x01, 0x02, 0x03]),
                string.Empty,
                string.Empty,
                string.Empty,
                Path.GetFullPath(path)));
        }

        public Task<FileNtfsMetadataDetails> GetNtfsMetadataDetailsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileNtfsMetadataDetails(
                FileAttributes.Archive,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }

        public Task<bool> SetNtfsAttributeFlagAsync(string path, FileAttributes flag, bool enabled, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return _service.GetCloudDiagnosticsAsync(path, cancellationToken);
        }

        public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        }

        public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("0", []));
        }

        public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, null, null));
        }

        public Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileThumbnailDiagnosticsDetails(null, string.Empty));
        }

        public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(new FileLockDiagnostics(
                inUse: false,
                lockBy: [],
                lockPids: [],
                lockServices: []));
        }
    }
}
