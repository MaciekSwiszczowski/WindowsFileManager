using Microsoft.Win32.SafeHandles;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.Storage.Provider;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;
using FileAttributes = System.IO.FileAttributes;

namespace WinUiFileManager.Diagnostics.Inspector;

public sealed class InspectorCloudDiagnosticsHandler : IDisposable
{
    private const FileAttributes FileAttributePinned = (FileAttributes)0x00080000;
    private const FileAttributes FileAttributeUnpinned = (FileAttributes)0x00100000;
    private const FileAttributes FileAttributeRecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes FileAttributeRecallOnDataAccess = (FileAttributes)0x00400000;
    private const uint PlaceholderStateNone = 0x00000000;
    private const uint PlaceholderStateInSync = 0x00000008;
    private const uint PlaceholderStatePartial = 0x00000010;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ICloudFilesInterop _cloudFilesInterop;
    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;
    private readonly ILogger<InspectorCloudDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorCloudDiagnosticsHandler(
        IMessenger messenger,
        ICloudFilesInterop cloudFilesInterop,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        ILogger<InspectorCloudDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _cloudFilesInterop = cloudFilesInterop;
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
        _logger = logger;
    }

    public void Initialize()
    {
        _messenger.Register<InspectorCloudDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => LoadAsync(message))));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private async Task<FileCloudDiagnosticsDetails> LoadAsync(InspectorCloudDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            var path = message.Path.DisplayPath;
            var attributes = File.GetAttributes(path);
            var syncRoot = await TryGetSyncRootInfoAsync(path).ConfigureAwait(false);
            var storageItem = await TryGetStorageItemAsync(path).ConfigureAwait(false);
            var provider = TryGetProviderDisplayName(storageItem);
            var available = TryGetAvailability(storageItem, timeoutCts.Token);
            var (syncState, transferState, customStatus) = await TryGetCloudPropertyValuesAsync(storageItem).ConfigureAwait(false);

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
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector cloud diagnostics for {Path}", message.Path.DisplayPath);
            return FileCloudDiagnosticsDetails.None;
        }
    }

    private uint TryGetPlaceholderState(SafeFileHandle handle, FileAttributes attributes)
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
                _ => string.Empty,
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryGetAvailability(IStorageItem? storageItem, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return storageItem is StorageFile file
                ? file.IsAvailable ? "Yes" : "No"
                : string.Empty;
        }
        catch
        {
            return string.Empty;
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
                    "System.Sync.Status",
                ]),
                StorageFolder folder => await folder.Properties.RetrievePropertiesAsync(
                [
                    "System.Sync.State",
                    "System.SyncTransferStatus",
                    "System.Sync.Status",
                ]),
                _ => null,
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
        FileAttributes attributes,
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
            (attributes & FileAttributes.Offline) != 0
            || (attributes & FileAttributeRecallOnOpen) != 0
            || (attributes & FileAttributeRecallOnDataAccess) != 0
            || (placeholderState & PlaceholderStatePartial) != 0;

        if (isDehydrated)
        {
            labels.Add("Dehydrated");
        }
        else if (placeholderState != PlaceholderStateNone
                 || (attributes & FileAttributes.ReparsePoint) != 0)
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
            _ => string.Empty,
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
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}
