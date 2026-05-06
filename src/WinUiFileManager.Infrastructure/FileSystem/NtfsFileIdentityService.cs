using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.FileSystem;

internal sealed class NtfsFileIdentityService : IFileIdentityService
{
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private const System.IO.FileAttributes FileAttributePinned = (System.IO.FileAttributes)0x00080000;
    private const System.IO.FileAttributes FileAttributeUnpinned = (System.IO.FileAttributes)0x00100000;
    private const System.IO.FileAttributes FileAttributeRecallOnOpen = (System.IO.FileAttributes)0x00040000;
    private const System.IO.FileAttributes FileAttributeRecallOnDataAccess = (System.IO.FileAttributes)0x00400000;
    private const uint PlaceholderStateNone = 0x00000000;
    private const uint PlaceholderStateInSync = 0x00000008;
    private const uint PlaceholderStatePartial = 0x00000010;

    internal static Func<IStorageItem?, CancellationToken, Task<(string SyncState, string TransferState, string CustomStatus)>> CloudPropertyValuesProvider { get; set; } =
        static (storageItem, _) => TryGetCloudPropertyValuesAsync(storageItem);

    private static readonly FileNtfsMetadataDetails EmptyNtfsMetadataDetails =
        new(0, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);

    private readonly IRestartManagerInterop _restartManagerInterop;
    private readonly ICloudFilesInterop _cloudFilesInterop;
    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;
    private readonly FileInspectorInteropOptions _interopOptions;

    public NtfsFileIdentityService(
        IRestartManagerInterop restartManagerInterop,
        ICloudFilesInterop cloudFilesInterop,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        FileInspectorInteropOptions? interopOptions = null)
    {
        _restartManagerInterop = restartManagerInterop;
        _cloudFilesInterop = cloudFilesInterop;
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
        _interopOptions = interopOptions ?? FileInspectorInteropOptions.AllEnabled;
    }

    public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Identity))
        {
            return Task.FromResult(NtfsFileId.None);
        }

        try
        {
            using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));
            if (!_fileSystemMetadataInterop.TryGetNtfsFileIdBytes(handle, out var bytes) || bytes is null)
            {
                return Task.FromResult(NtfsFileId.None);
            }

            return Task.FromResult(new NtfsFileId(bytes));
        }
        catch
        {
            return Task.FromResult(NtfsFileId.None);
        }
    }

    public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Identity))
        {
            return Task.FromResult(new FileIdentityDetails(
                NtfsFileId.None,
                string.Empty,
                string.Empty,
                string.Empty,
                path));
        }

        try
        {
            using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));
            var fileId = _fileSystemMetadataInterop.TryGetNtfsFileIdBytes(handle, out var idBytes) && idBytes is not null
                ? new NtfsFileId(idBytes)
                : NtfsFileId.None;

            var hasLegacy = _fileSystemMetadataInterop.TryGetLegacyFileIndex(handle, out var legacyInfo, out var legacyError);
            if (!hasLegacy)
            {
                _ = legacyError;
            }

            var root = Path.GetPathRoot(Path.GetFullPath(path));
            var volumeSerial = _fileSystemMetadataInterop.TryGetVolumeSerialHex(root ?? string.Empty);
            var finalPath = _fileSystemMetadataInterop.TryGetFinalPath(handle) ?? Path.GetFullPath(path);

            return Task.FromResult(new FileIdentityDetails(
                fileId,
                volumeSerial ?? string.Empty,
                hasLegacy
                    ? $"0x{((ulong)legacyInfo.FileIndexHigh << 32 | legacyInfo.FileIndexLow):X16}"
                    : string.Empty,
                hasLegacy ? legacyInfo.NumberOfLinks.ToString() : string.Empty,
                finalPath));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new FileIdentityDetails(
                NtfsFileId.None,
                string.Empty,
                string.Empty,
                string.Empty,
                path))
            ;
        }
    }

    public Task<FileNtfsMetadataDetails> GetNtfsMetadataDetailsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Identity))
        {
            return Task.FromResult(EmptyNtfsMetadataDetails);
        }

        try
        {
            using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));
            if (!_fileSystemMetadataInterop.TryGetFileBasicInfo(handle, out var basicInfo))
            {
                throw new InvalidOperationException("GetFileInformationByHandleEx(FileBasicInfo) failed.");
            }

            return Task.FromResult(new FileNtfsMetadataDetails(
                (System.IO.FileAttributes)basicInfo.FileAttributes,
                FromFileTimeUtc(basicInfo.CreationTime),
                FromFileTimeUtc(basicInfo.LastAccessTime),
                FromFileTimeUtc(basicInfo.LastWriteTime),
                FromFileTimeUtc(basicInfo.ChangeTime)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fallback = GetFallbackNtfsMetadata(path);
            return Task.FromResult(fallback);
        }
    }

    public Task<bool> SetNtfsAttributeFlagAsync(string path, System.IO.FileAttributes flag, bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var currentAttributes = File.GetAttributes(path);
            var updatedAttributes = enabled
                ? currentAttributes | flag
                : currentAttributes & ~flag;

            if (updatedAttributes != currentAttributes)
            {
                File.SetAttributes(path, updatedAttributes);
            }

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Cloud))
        {
            return FileCloudDiagnosticsDetails.None;
        }

        try
        {
            var attributes = File.GetAttributes(path);
            var syncRoot = await TryGetSyncRootInfoAsync(path);
            var storageItem = await TryGetStorageItemAsync(path);
            var provider = TryGetProviderDisplayName(storageItem);
            var available = await TryGetAvailabilityAsync(storageItem, cancellationToken);
            var (syncState, transferState, customStatus) = await CloudPropertyValuesProvider(storageItem, cancellationToken);

            using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));
            var placeholderState = TryGetPlaceholderState(handle, attributes);
            var status = BuildCloudStatus(attributes, placeholderState, syncState, transferState, customStatus);
            var syncRootPath = syncRoot?.Path?.Path ?? string.Empty;
            var syncRootId = syncRoot?.Id ?? string.Empty;
            var providerId = syncRoot is null ? string.Empty : syncRoot.ProviderId.ToString();
            var isCloudControlled =
                !string.IsNullOrWhiteSpace(syncRootId)
                || !string.IsNullOrWhiteSpace(provider)
                || placeholderState != PlaceholderStateNone;

            return isCloudControlled
                ? new FileCloudDiagnosticsDetails(
                    true,
                    status,
                    provider,
                    syncRootPath,
                    syncRootId,
                    providerId,
                    available,
                    transferState,
                    customStatus)
                : FileCloudDiagnosticsDetails.None;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return FileCloudDiagnosticsDetails.None;
        }
    }

    public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Links))
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        }

        try
        {
            FileSystemInfo fsi = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            var linkTarget = fsi.LinkTarget ?? string.Empty;
            var linkStatus = string.Empty;
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                linkStatus = "Shell shortcut";
            }

            var reparseTag = fsi.Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint)
                ? "Reparse point"
                : string.Empty;

            return Task.FromResult(new FileLinkDiagnosticsDetails(
                linkTarget,
                linkStatus,
                reparseTag,
                string.Empty,
                string.Empty));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        }
    }

    public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Streams))
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("0", []));
        }

        try
        {
            var streams = _fileSystemMetadataInterop.EnumerateAlternateDataStreamDisplayLines(path);
            return Task.FromResult(new FileStreamDiagnosticsDetails(streams.Count.ToString(), streams));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("0", []));
        }
    }

    public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Security))
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null));
        }

        try
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

            var owner = SafeIdentityToString(() => security.GetOwner(typeof(NTAccount)));
            var group = SafeIdentityToString(() => security.GetGroup(typeof(NTAccount)));
            var daclSummary = SummarizeAccessRules(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());

            string saclSummary;
            try
            {
                saclSummary = SummarizeAuditRules(security.GetAuditRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAuditRule>());
            }
            catch
            {
                saclSummary = string.Empty;
            }

            return Task.FromResult(new FileSecurityDiagnosticsDetails(
                owner,
                group,
                daclSummary,
                saclSummary,
                security.AreAccessRulesProtected ? false : true,
                security.AreAccessRulesProtected));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, null, null));
        }
    }

    public async Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Thumbnails))
        {
            return new FileThumbnailDiagnosticsDetails(null, string.Empty);
        }

        try
        {
            var progId = Path.GetExtension(path);
            byte[]? thumbnailBytes = null;

            IStorageItem? storageItem = null;
            if (File.Exists(path))
            {
                storageItem = await StorageFile.GetFileFromPathAsync(path);
            }
            else if (Directory.Exists(path))
            {
                storageItem = await StorageFolder.GetFolderFromPathAsync(path);
            }

            if (storageItem is not null)
            {
                using var thumbnail = storageItem is StorageFile file
                    ? await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(cancellationToken)
                    : await ((StorageFolder)storageItem).GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(cancellationToken);

                if (thumbnail is not null)
                {
                    thumbnail.Seek(0);
                    using var input = thumbnail.AsStreamForRead();
                    using var memory = new MemoryStream();
                    await input.CopyToAsync(memory, cancellationToken);
                    thumbnailBytes = memory.ToArray();
                }
            }

            return new FileThumbnailDiagnosticsDetails(thumbnailBytes, progId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new FileThumbnailDiagnosticsDetails(null, string.Empty);
        }
    }

    public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsInteropEnabled(FileInspectorInteropCategories.Locks))
        {
            return Task.FromResult(FileLockDiagnostics.None);
        }

        try
        {
            var lockBy = new List<string>();
            var lockPids = new List<int>();
            var lockServices = new List<string>();
            var inUse = TryGetRestartManagerLocks(path, lockBy, lockPids, lockServices);

            return Task.FromResult(new FileLockDiagnostics(
                inUse: inUse,
                lockBy: lockBy,
                lockPids: lockPids,
                lockServices: lockServices));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(FileLockDiagnostics.None);
        }
    }

    private bool IsInteropEnabled(FileInspectorInteropCategories category) =>
        _interopOptions.IsEnabled(category);

    private static FileNtfsMetadataDetails GetFallbackNtfsMetadata(string path)
    {
        FileSystemInfo info = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
        info.Refresh();
        return new FileNtfsMetadataDetails(
            info.Attributes,
            info.CreationTimeUtc,
            info.LastAccessTimeUtc,
            info.LastWriteTimeUtc,
            DateTime.MinValue);
    }

    private uint TryGetPlaceholderState(SafeFileHandle handle, System.IO.FileAttributes attributes)
    {
        if (!_fileSystemMetadataInterop.TryGetFileAttributeReparseTag(handle, out var reparseTag))
        {
            return PlaceholderStateNone;
        }

        return _cloudFilesInterop.GetPlaceholderState((uint)attributes, reparseTag);
    }

    private static async Task<StorageProviderSyncRootInfo?> TryGetSyncRootInfoAsync(string path)
    {
        var folderPath = Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path);

        while (!string.IsNullOrWhiteSpace(folderPath))
        {
            try
            {
                var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                return StorageProviderSyncRootManager.GetSyncRootInformationForFolder(folder);
            }
            catch
            {
                folderPath = Path.GetDirectoryName(folderPath);
            }
        }

        return null;
    }

    private static async Task<IStorageItem?> TryGetStorageItemAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return await StorageFile.GetFileFromPathAsync(path);
            }

            if (Directory.Exists(path))
            {
                return await StorageFolder.GetFolderFromPathAsync(path);
            }
        }
        catch
        {
        }

        return null;
    }

    private static string TryGetProviderDisplayName(IStorageItem? storageItem)
    {
        try
        {
            return storageItem switch
            {
                StorageFile file => file.Provider?.DisplayName ?? string.Empty,
                StorageFolder folder => folder.Provider?.DisplayName ?? string.Empty,
                _ => string.Empty
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Task<string> TryGetAvailabilityAsync(IStorageItem? storageItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult(storageItem switch
            {
                StorageFile file => file.IsAvailable ? "Yes" : "No",
                _ => string.Empty
            });
        }
        catch
        {
            return Task.FromResult(string.Empty);
        }
    }

    private static async Task<(string SyncState, string TransferState, string CustomStatus)> TryGetCloudPropertyValuesAsync(IStorageItem? storageItem)
    {
        if (storageItem is null)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        try
        {
            var rawValues = storageItem switch
            {
                StorageFile file => await file.Properties.RetrievePropertiesAsync(
                [
                    "System.Sync.State",
                    "System.SyncTransferStatus",
                    "System.Sync.Status"
                ]),
                StorageFolder folder => await folder.Properties.RetrievePropertiesAsync(
                [
                    "System.Sync.State",
                    "System.SyncTransferStatus",
                    "System.Sync.Status"
                ]),
                _ => null
            };

            if (rawValues is null)
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var syncState = TryFormatSyncState(rawValues.TryGetValue("System.Sync.State", out var syncStateValue) ? syncStateValue : null);
            var transferState = TryFormatTransferState(rawValues.TryGetValue("System.SyncTransferStatus", out var transferStateValue) ? transferStateValue : null);
            var customStatus = rawValues.TryGetValue("System.Sync.Status", out var customValue)
                ? customValue?.ToString() ?? string.Empty
                : string.Empty;

            return (syncState, transferState, customStatus);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    private static string BuildCloudStatus(
        System.IO.FileAttributes attributes,
        uint placeholderState,
        string syncState,
        string transferState,
        string customStatus)
    {
        var labels = new List<string>();

        if ((attributes & FileAttributePinned) != 0)
        {
            labels.Add("Pinned");
        }

        if ((attributes & FileAttributeUnpinned) != 0)
        {
            labels.Add("Unpinned");
        }

        var isDehydrated =
            (attributes & System.IO.FileAttributes.Offline) != 0
            || (attributes & FileAttributeRecallOnOpen) != 0
            || (attributes & FileAttributeRecallOnDataAccess) != 0
            || (placeholderState & PlaceholderStatePartial) != 0;

        if (isDehydrated)
        {
            labels.Add("Dehydrated");
        }
        else if (placeholderState != PlaceholderStateNone
                 || (attributes & System.IO.FileAttributes.ReparsePoint) != 0)
        {
            labels.Add("Hydrated");
        }

        if ((placeholderState & PlaceholderStateInSync) != 0
            || string.Equals(syncState, "Synced", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("Synced");
        }
        else if (!string.IsNullOrWhiteSpace(syncState))
        {
            labels.Add(syncState);
        }

        if (!string.IsNullOrWhiteSpace(transferState))
        {
            labels.Add(transferState);
        }

        if (!string.IsNullOrWhiteSpace(customStatus))
        {
            labels.Add(customStatus);
        }

        return string.Join(", ", labels.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string TryFormatSyncState(object? rawValue)
    {
        return TryConvertToUInt32(rawValue) switch
        {
            0 => "Not set up",
            1 => "Not run",
            2 => "Synced",
            3 => "Sync errors",
            4 => "Pending",
            5 => "Syncing",
            _ => string.Empty
        };
    }

    private static string TryFormatTransferState(object? rawValue)
    {
        var value = TryConvertToUInt32(rawValue);
        if (value is null or 0)
        {
            return string.Empty;
        }

        var labels = new List<string>();
        if ((value & 0x00000001) != 0)
        {
            labels.Add("Upload pending");
        }

        if ((value & 0x00000002) != 0)
        {
            labels.Add("Download pending");
        }

        if ((value & 0x00000004) != 0)
        {
            labels.Add("Transferring");
        }

        if ((value & 0x00000008) != 0)
        {
            labels.Add("Paused");
        }

        if ((value & 0x00000010) != 0)
        {
            labels.Add("Error");
        }

        if ((value & 0x00000020) != 0)
        {
            labels.Add("Fetching metadata");
        }

        if ((value & 0x00000080) != 0)
        {
            labels.Add("Warning");
        }

        return string.Join(", ", labels);
    }

    private static uint? TryConvertToUInt32(object? rawValue)
    {
        try
        {
            return rawValue switch
            {
                byte value => value,
                ushort value => value,
                uint value => value,
                ulong value => checked((uint)value),
                int value when value >= 0 => (uint)value,
                long value when value >= 0 => checked((uint)value),
                string value when uint.TryParse(value, out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static DateTime FromFileTimeUtc(long fileTime) =>
        fileTime <= 0 ? DateTime.MinValue : DateTime.FromFileTimeUtc(fileTime);

    private bool? TryGetRestartManagerLocks(
        string path,
        List<string> lockBy,
        List<int> lockPids,
        List<string> lockServices)
    {
        var startResult = _restartManagerInterop.StartSession(out var sessionHandle);
        if (startResult != ErrorSuccess)
        {
            return null;
        }

        try
        {
            var resources = new[] { path };
            var registerResult = _restartManagerInterop.RegisterResources(sessionHandle, resources);

            if (registerResult != ErrorSuccess)
            {
                return null;
            }

            uint processInfoNeeded = 0;
            uint processInfo = 0;
            uint rebootReasons;

            var listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                null,
                out rebootReasons);

            if (listResult != ErrorSuccess && listResult != ErrorMoreData)
            {
                return null;
            }

            if (processInfoNeeded == 0)
            {
                return false;
            }

            processInfo = processInfoNeeded;
            var processInfos = new RestartManagerProcessInfo[processInfoNeeded];
            listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                processInfos,
                out rebootReasons);

            if (listResult != ErrorSuccess)
            {
                return null;
            }

            for (var i = 0; i < processInfo; i++)
            {
                var processInfoItem = processInfos[i];
                var appName = string.IsNullOrWhiteSpace(processInfoItem.AppName)
                    ? $"PID {processInfoItem.ProcessId}"
                    : processInfoItem.AppName.Trim();

                lockBy.Add(appName);

                if (processInfoItem.ProcessId > 0)
                {
                    lockPids.Add(processInfoItem.ProcessId);
                }

                if (!string.IsNullOrWhiteSpace(processInfoItem.ServiceShortName))
                {
                    lockServices.Add(processInfoItem.ServiceShortName.Trim());
                }
            }

            Deduplicate(lockBy);
            Deduplicate(lockPids);
            Deduplicate(lockServices);
            return lockBy.Count > 0 || lockPids.Count > 0 || lockServices.Count > 0;
        }
        finally
        {
            _ = _restartManagerInterop.EndSession(sessionHandle);
        }
    }

    private static void Deduplicate<T>(List<T> source)
    {
        if (source.Count <= 1)
        {
            return;
        }

        var set = new HashSet<T>();
        var index = 0;
        foreach (var item in source)
        {
            if (set.Add(item))
            {
                source[index++] = item;
            }
        }

        if (index < source.Count)
        {
            source.RemoveRange(index, source.Count - index);
        }
    }

    private static string SafeIdentityToString(Func<IdentityReference?> factory)
    {
        try
        {
            return factory()?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SummarizeAccessRules(IEnumerable<FileSystemAccessRule> rules)
    {
        var allow = 0;
        var deny = 0;
        var inherited = 0;

        foreach (var rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow)
            {
                allow++;
            }
            else
            {
                deny++;
            }

            if (rule.IsInherited)
            {
                inherited++;
            }
        }

        if (allow == 0 && deny == 0)
        {
            return string.Empty;
        }

        return $"Allow {allow}, Deny {deny}, Inherited {inherited}";
    }

    private static string SummarizeAuditRules(IEnumerable<FileSystemAuditRule> rules)
    {
        var success = 0;
        var failure = 0;

        foreach (var rule in rules)
        {
            if ((rule.AuditFlags & AuditFlags.Success) != 0)
            {
                success++;
            }

            if ((rule.AuditFlags & AuditFlags.Failure) != 0)
            {
                failure++;
            }
        }

        if (success == 0 && failure == 0)
        {
            return string.Empty;
        }

        return $"Success {success}, Failure {failure}";
    }
}
