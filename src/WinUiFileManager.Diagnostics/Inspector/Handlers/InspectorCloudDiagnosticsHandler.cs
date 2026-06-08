using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Windows.Storage;
using Windows.Storage.Provider;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Diagnostics.Inspector;
using FileAttributes = System.IO.FileAttributes;

namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> with a
/// path's cloud/placeholder state: provider name, sync-root identity, pin/hydration status, availability,
/// and sync/transfer states, by combining file attributes, the CldApi placeholder state, and WinRT
/// <see cref="StorageProviderSyncRootManager"/> / Shell sync properties.
/// </summary>
/// <remarks>
/// <see cref="LoadAsync"/> runs off the UI thread and awaits WinRT Storage APIs with
/// <c>ConfigureAwait(false)</c>; these WinRT calls are usable off the UI/STA thread.
/// </remarks>
public sealed class InspectorCloudDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        FileCloudDiagnosticsDetails,
        InspectorCloudDiagnosticsResponseMessage>
{
    // Cloud-files attribute flags not exposed by System.IO.FileAttributes (FILE_ATTRIBUTE_PINNED, etc.).
    private const FileAttributes FileAttributePinned = (FileAttributes)0x00080000;
    private const FileAttributes FileAttributeUnpinned = (FileAttributes)0x00100000;
    private const FileAttributes FileAttributeRecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes FileAttributeRecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes CloudEvidenceAttributes =
        FileAttributePinned
        | FileAttributeUnpinned
        | FileAttributeRecallOnOpen
        | FileAttributeRecallOnDataAccess;
    // CF_PLACEHOLDER_STATE bit flags returned by CfGetPlaceholderStateFromAttributeTag.
    private const uint PlaceholderStateNone = 0x00000000;
    private const uint PlaceholderStateInSync = 0x00000008;
    private const uint PlaceholderStatePartial = 0x00000010;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);
    // Shared, immutable key set for the Shell sync-property query — avoids allocating a new string[] per inspection.
    private static readonly string[] SyncPropertyKeys =
    [
        "System.Sync.State",
        "System.SyncTransferStatus",
        "System.Sync.Status",
    ];

    private readonly ICloudFilesInterop _cloudFilesInterop;
    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;
    private readonly StorageProviderSyncRootCache _syncRootCache;

    public InspectorCloudDiagnosticsHandler(
        IMessenger messenger,
        ICloudFilesInterop cloudFilesInterop,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        StorageProviderSyncRootCache syncRootCache,
        ILogger<InspectorCloudDiagnosticsHandler> logger,
        Func<FileCloudDiagnosticsDetails, InspectorCloudDiagnosticsResponseMessage> responseFactory)
        : base(messenger, logger, responseFactory)
    {
        _cloudFilesInterop = cloudFilesInterop;
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
        _syncRootCache = syncRootCache;
    }

    /// <summary>
    /// Gathers cloud/placeholder diagnostics for the requested path from several sources and merges them.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>
    /// Cloud details when the path is cloud-controlled (has a sync root, a provider, or a placeholder
    /// state); otherwise <see cref="FileCloudDiagnosticsDetails.None"/>. Also returns <c>None</c> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to None by the base class.</remarks>
    protected override async Task<FileCloudDiagnosticsDetails> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.CancelAfter(LoadTimeout);

        var path = message.Path.DisplayPath;
        var attributes = File.GetAttributes(path);
        var syncRoot = _syncRootCache.FindForPath(path);
        var hasCloudAttributeEvidence = (attributes & CloudEvidenceAttributes) != 0;
        var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

        if (syncRoot is null && !hasCloudAttributeEvidence && !isReparsePoint)
        {
            return FileCloudDiagnosticsDetails.None;
        }

        // Live Shell queries (StorageItem acquisition, Provider, availability, RetrievePropertiesAsync) are the
        // dominant managed+native allocation on this path. Gate them behind actual cloud evidence — a plain local
        // file that merely sits inside a sync-root folder needs no live provider/sync-state lookup; its sync-root
        // identity (id/provider id) still comes cheaply from the cache below.
        var storageItem = hasCloudAttributeEvidence || isReparsePoint
            ? await TryGetStorageItemAsync(path).ConfigureAwait(false)
            : null;
        var provider = storageItem is null ? string.Empty : TryGetProviderDisplayName(storageItem);
        var available = storageItem is null ? string.Empty : TryGetAvailability(storageItem, timeoutCts.Token);
        var (syncState, transferState, customStatus) = await TryGetCloudPropertyValuesAsync(storageItem).ConfigureAwait(false);

        var placeholderState = isReparsePoint ? TryGetPlaceholderState(path, attributes) : PlaceholderStateNone;
        var status = BuildCloudStatus(attributes, placeholderState, syncState, transferState, customStatus);
        var syncRootPath = string.Empty;
        var syncRootId = string.Empty;
        var providerId = string.Empty;
        if (syncRoot is { } matchedSyncRoot)
        {
            syncRootPath = matchedSyncRoot.Path;
            syncRootId = matchedSyncRoot.Id;
            providerId = matchedSyncRoot.ProviderId;
        }

        var isCloudControlled =
            !string.IsNullOrWhiteSpace(syncRootId)
            || !string.IsNullOrWhiteSpace(provider)
            || placeholderState != PlaceholderStateNone
            || hasCloudAttributeEvidence;

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

    protected override FileCloudDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileCloudDiagnosticsDetails.None;

    /// <summary>
    /// Maps the file's reparse tag + attributes to a CldApi placeholder-state bitmask, or
    /// <see cref="PlaceholderStateNone"/> when no reparse tag is present.
    /// </summary>
    private uint TryGetPlaceholderState(string path, FileAttributes attributes)
    {
        using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, attributes.HasFlag(FileAttributes.Directory));
        if (!_fileSystemMetadataInterop.TryGetFileAttributeReparseTag(handle, out var reparseTag))
        {
            return PlaceholderStateNone;
        }

        return _cloudFilesInterop.GetPlaceholderState((uint)attributes, reparseTag);
    }

    /// <summary>
    /// Opens the path as a WinRT <see cref="StorageFile"/> or <see cref="StorageFolder"/>, returning null
    /// if it is neither or cannot be opened.
    /// </summary>
    /// <remarks>Best-effort: failures are swallowed; the caller treats null as "no cloud info".</remarks>
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
            // ignored
        }

        return null;
    }

    /// <summary>Returns the cloud provider's display name for the item, or empty if unavailable.</summary>
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

    /// <summary>Reports whether a cloud file is locally available ("Yes"/"No"), or empty for folders/unknown.</summary>
    /// <param name="cancellationToken">Honored before the (potentially blocking) availability probe.</param>
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

    /// <summary>
    /// Reads the Shell sync properties (<c>System.Sync.State</c>, <c>System.SyncTransferStatus</c>,
    /// <c>System.Sync.Status</c>) and formats them, returning empties when unavailable.
    /// </summary>
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
                StorageFile file => await file.Properties.RetrievePropertiesAsync(SyncPropertyKeys),
                StorageFolder folder => await folder.Properties.RetrievePropertiesAsync(SyncPropertyKeys),
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

    /// <summary>
    /// Composes a human-readable, comma-separated status from attributes, placeholder state, and the Shell
    /// sync/transfer/custom values (e.g. "Pinned, Hydrated, Synced").
    /// </summary>
    /// <remarks>
    /// Hydration is inferred from offline/recall attributes and the partial-placeholder bit; "Synced" is
    /// derived from either the in-sync placeholder bit or the Shell sync state. Labels are de-duplicated
    /// case-insensitively because the same state can be implied by more than one source.
    /// </remarks>
    private static string BuildCloudStatus(FileAttributes attributes, uint placeholderState, string syncState, string transferState, string customStatus)
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
        else
        {
            if (placeholderState != PlaceholderStateNone || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                labels.Add("Hydrated");
            }
        }

        if ((placeholderState & PlaceholderStateInSync) != 0 || string.Equals(syncState, "Synced", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Maps the numeric <c>System.Sync.State</c> value to a label (empty if unrecognized).</summary>
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

    /// <summary>Decodes the <c>System.SyncTransferStatus</c> bit flags into a comma-separated label list.</summary>
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

    /// <summary>
    /// Coerces a boxed Shell property value (various integer types or a numeric string) to
    /// <see cref="uint"/>, returning null when it cannot be represented.
    /// </summary>
    /// <remarks>Property values arrive boxed with a provider-dependent runtime type, hence the broad switch.</remarks>
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
                int value and >= 0 => (uint)value,
                long value and >= 0 => checked((uint)value),
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
