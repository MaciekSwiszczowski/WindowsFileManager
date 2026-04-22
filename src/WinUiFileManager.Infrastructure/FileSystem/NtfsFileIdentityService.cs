using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.SafeHandles;

namespace WinUiFileManager.Infrastructure.FileSystem;

internal sealed class NtfsFileIdentityService : IFileIdentityService
{
    private const int FindStreamInfoStandard = 0;
    private const uint GetFinalPathNameNormalized = 0;
    private const uint FileReadAttributesAccess = 0x80;
    private const System.IO.FileAttributes FileAttributePinned = (System.IO.FileAttributes)0x00080000;
    private const System.IO.FileAttributes FileAttributeUnpinned = (System.IO.FileAttributes)0x00100000;
    private const System.IO.FileAttributes FileAttributeRecallOnOpen = (System.IO.FileAttributes)0x00040000;
    private const System.IO.FileAttributes FileAttributeRecallOnDataAccess = (System.IO.FileAttributes)0x00400000;
    private const uint PlaceholderStateNone = 0x00000000;
    private const uint PlaceholderStateInSync = 0x00000008;
    private const uint PlaceholderStatePartial = 0x00000010;

    internal static Func<IStorageItem?, CancellationToken, Task<(string SyncState, string TransferState, string CustomStatus)>> CloudPropertyValuesProvider { get; set; } =
        static (storageItem, _) => TryGetCloudPropertyValuesAsync(storageItem);

    private readonly IFileIdentityInterop _fileIdentityInterop;
    private readonly ICloudFilesInterop _cloudFilesInterop;

    public NtfsFileIdentityService(
        IFileIdentityInterop fileIdentityInterop,
        ICloudFilesInterop cloudFilesInterop)
    {
        _fileIdentityInterop = fileIdentityInterop;
        _cloudFilesInterop = cloudFilesInterop;
    }

    public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var handle = OpenMetadataHandle(path);
            return Task.FromResult(GetFileIdFromHandle(handle));
        }
        catch
        {
            return Task.FromResult(NtfsFileId.None);
        }
    }

    public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var handle = OpenMetadataHandle(path);
            var fileId = GetFileIdFromHandle(handle);

            var legacyInfo = TryGetLegacyFileInfo(handle, out var legacyError);
            if (!string.IsNullOrWhiteSpace(legacyError))
            {
                _ = legacyError;
            }

            var volumeSerial = TryGetVolumeSerial(path);
            var finalPath = TryGetFinalPath(handle) ?? Path.GetFullPath(path);

            return Task.FromResult(new FileIdentityDetails(
                fileId,
                volumeSerial ?? string.Empty,
                legacyInfo is null
                    ? string.Empty
                    : $"0x{((ulong)legacyInfo.Value.nFileIndexHigh << 32 | legacyInfo.Value.nFileIndexLow):X16}",
                legacyInfo is null ? string.Empty : legacyInfo.Value.nNumberOfLinks.ToString(),
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

        try
        {
            using var handle = OpenMetadataHandle(path);
            unsafe
            {
                FILE_BASIC_INFO basicInfo;
                if (!PInvoke.GetFileInformationByHandleEx(
                        new HANDLE(handle.DangerousGetHandle()),
                        FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo,
                        &basicInfo,
                        (uint)sizeof(FILE_BASIC_INFO)))
                {
                    throw new InvalidOperationException($"GetFileInformationByHandleEx failed: {Marshal.GetLastPInvokeError()}");
                }

                return Task.FromResult(new FileNtfsMetadataDetails(
                    (System.IO.FileAttributes)basicInfo.FileAttributes,
                    FromFileTimeUtc(basicInfo.CreationTime),
                    FromFileTimeUtc(basicInfo.LastAccessTime),
                    FromFileTimeUtc(basicInfo.LastWriteTime),
                    FromFileTimeUtc(basicInfo.ChangeTime)));
            }
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

        try
        {
            var attributes = File.GetAttributes(path);
            var syncRoot = await TryGetSyncRootInfoAsync(path);
            var storageItem = await TryGetStorageItemAsync(path);
            var provider = TryGetProviderDisplayName(storageItem);
            var available = await TryGetAvailabilityAsync(storageItem, cancellationToken);
            var (syncState, transferState, customStatus) = await CloudPropertyValuesProvider(storageItem, cancellationToken);

            using var handle = OpenMetadataHandle(path);
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

        try
        {
            var streams = new List<string>();
            unsafe
            {
                WIN32_FIND_STREAM_DATA data;
                fixed (char* pathPointer = path)
                {
                    using var handle = new SafeFindFilesHandle(
                        (IntPtr)PInvoke.FindFirstStream(pathPointer, STREAM_INFO_LEVELS.FindStreamInfoStandard, &data, 0),
                        ownsHandle: true);
                    if (handle.IsInvalid)
                    {
                        return Task.FromResult(new FileStreamDiagnosticsDetails("0", streams));
                    }

                    AddStreamName(streams, data);
                    while (PInvoke.FindNextStream(new HANDLE(handle.DangerousGetHandle()), &data))
                    {
                        AddStreamName(streams, data);
                    }
                }
            }

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

        var result = _fileIdentityInterop.GetLockDiagnostics(path);
        if (!result.Success)
        {
            return Task.FromResult(FileLockDiagnostics.None);
        }

        var diagnostics = new FileLockDiagnostics(
            inUse: result.InUse,
            lockBy: result.LockBy,
            lockPids: result.LockPids,
            lockServices: result.LockServices,
            usage: result.Usage,
            canSwitchTo: result.CanSwitchTo,
            canClose: result.CanClose);

        return Task.FromResult(diagnostics);
    }

    private static SafeFileHandle OpenMetadataHandle(string path)
    {
        var flags = Directory.Exists(path)
            ? FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
            : 0;
        var handle = PInvoke.CreateFile(
            path,
            FileReadAttributesAccess,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            flags,
            null);

        if (handle.IsInvalid)
        {
            throw new InvalidOperationException($"CreateFileW failed: {Marshal.GetLastPInvokeError()}");
        }

        return handle;
    }

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
        unsafe
        {
            FILE_ATTRIBUTE_TAG_INFO tagInfo;
            if (!PInvoke.GetFileInformationByHandleEx(
                    new HANDLE(handle.DangerousGetHandle()),
                    FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo,
                    &tagInfo,
                    (uint)sizeof(FILE_ATTRIBUTE_TAG_INFO)))
            {
                return PlaceholderStateNone;
            }

            return _cloudFilesInterop.GetPlaceholderState((uint)attributes, tagInfo.ReparseTag);
        }
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

    private static NtfsFileId GetFileIdFromHandle(SafeFileHandle handle)
    {
        unsafe
        {
            FILE_ID_INFO fileIdInfo;
            if (!PInvoke.GetFileInformationByHandleEx(
                    new HANDLE(handle.DangerousGetHandle()),
                    FILE_INFO_BY_HANDLE_CLASS.FileIdInfo,
                    &fileIdInfo,
                    (uint)sizeof(FILE_ID_INFO)))
            {
                return NtfsFileId.None;
            }

            var identifier = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<Windows.Win32.__byte_16, byte>(ref fileIdInfo.FileId.Identifier),
                16);
            var bytes = new byte[identifier.Length];
            identifier.CopyTo(bytes);
            return new NtfsFileId(bytes);
        }
    }

    private static BY_HANDLE_FILE_INFORMATION? TryGetLegacyFileInfo(
        SafeFileHandle handle,
        out string? error)
    {
        error = null;
        if (!PInvoke.GetFileInformationByHandle(handle, out var info))
        {
            error = Marshal.GetLastPInvokeError().ToString();
            return null;
        }

        return info;
    }

    private static string? TryGetVolumeSerial(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        unsafe
        {
            uint serial = 0;
            uint maximumComponentLength = 0;
            fixed (char* rootPath = root)
            {
                if (!PInvoke.GetVolumeInformation(rootPath, null, 0, &serial, &maximumComponentLength, null, null, 0))
                {
                    return null;
                }
            }

            return serial.ToString("X8");
        }
    }

    private static unsafe string? TryGetFinalPath(SafeFileHandle handle)
    {
        Span<char> buffer = stackalloc char[1024];
        fixed (char* bufferPointer = buffer)
        {
            var len = PInvoke.GetFinalPathNameByHandle(new HANDLE(handle.DangerousGetHandle()), bufferPointer, (uint)buffer.Length, GetFinalPathNameNormalized);
            if (len == 0)
            {
                return null;
            }

            var sliceLength = (int)Math.Min(len, (uint)buffer.Length);
            return GetNullTerminatedString(buffer[..sliceLength]);
        }
    }

    private static void AddStreamName(List<string> streams, WIN32_FIND_STREAM_DATA data)
    {
        var streamName = GetNullTerminatedString(data.cStreamName, 296);
        if (string.IsNullOrWhiteSpace(streamName))
        {
            return;
        }

        if (string.Equals(streamName, "::$DATA", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        streams.Add($"{streamName.Trim()} ({data.StreamSize} bytes)");
    }

    private static string GetNullTerminatedString(Windows.Win32.__char_296 buffer, int length)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<Windows.Win32.__char_296, char>(ref buffer),
            length);
        return GetNullTerminatedString(span);
    }

    private static string GetNullTerminatedString(ReadOnlySpan<char> buffer)
    {
        var terminatorIndex = buffer.IndexOf('\0');
        var value = terminatorIndex >= 0 ? buffer[..terminatorIndex] : buffer;
        return value.ToString();
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
