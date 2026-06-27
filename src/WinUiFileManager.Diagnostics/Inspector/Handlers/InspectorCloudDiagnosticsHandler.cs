using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Application.Diagnostics.Profiling;

namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> with a
/// path's cloud/placeholder state: provider name, sync-root identity, and pin/hydration status, by combining file
/// attributes, the CldApi placeholder state, and the registered sync-root snapshot.
/// </summary>
/// <remarks>
/// <para>
/// All cloud data comes from cheap, non-COM sources with no finalizer-bound runtime-callable wrappers: file
/// attributes (BCL), placeholder state (CldApi via <see cref="ICloudFilesInterop"/>), and the registry-sourced
/// sync-root snapshot (provider id + display name). The earlier Shell property-store read of the live
/// <c>System.Sync.*</c> values was removed: it entered the Shell/COM property system per selection
/// (<c>SHGetPropertyStoreFromParsingName</c>) and leaked process-lifetime native allocations inside Shell internals
/// that callers cannot release, for a non-critical diagnostic.
/// </para>
/// <para>
/// This type <em>loads the facts</em>; turning the attribute/placeholder flags into the human-readable status label
/// is delegated to <see cref="CloudStatusFormatter"/>, which also owns the cloud-file flag constants and is
/// unit-tested in isolation (the flag semantics are subtle — see the CF_PLACEHOLDER_STATE notes there).
/// </para>
/// <para>
/// <see cref="LoadAsync"/> runs off the UI thread (on the base class's <c>Task.Run</c> thread-pool thread) and relies
/// on the base's latest-request-wins suppression rather than a timeout.
/// </para>
/// </remarks>
public sealed class InspectorCloudDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        FileCloudDiagnosticsDetails,
        InspectorCloudDiagnosticsResponseMessage>
{
    private readonly ICloudFilesInterop _cloudFilesInterop;
    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;
    private readonly StorageProviderSyncRootCache _syncRootCache;

    public InspectorCloudDiagnosticsHandler(
        IMessenger messenger,
        ICloudFilesInterop cloudFilesInterop,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        StorageProviderSyncRootCache syncRootCache,
        ILogger<InspectorCloudDiagnosticsHandler> logger,
        Func<FileCloudDiagnosticsDetails, InspectorCloudDiagnosticsResponseMessage> responseFactory,
        IInspectorDiagnosticsGate diagnosticsGate)
        : base(messenger, logger, responseFactory, diagnosticsGate)
    {
        _cloudFilesInterop = cloudFilesInterop;
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
        _syncRootCache = syncRootCache;
    }

    protected override DiagnosticsCategory Category => DiagnosticsCategory.Cloud;

    /// <summary>
    /// Gathers cloud/placeholder diagnostics for the requested path from several sources and merges them.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>
    /// Cloud details when the path is cloud-controlled (has a sync root, a provider, or a placeholder
    /// state); otherwise <see cref="FileCloudDiagnosticsDetails.None"/>. Also returns <c>None</c> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to None by the base class.</remarks>
    protected override Task<FileCloudDiagnosticsDetails> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        var path = message.Path.DisplayPath;
        var attributes = File.GetAttributes(path);
        var syncRoot = _syncRootCache.FindForPath(path);
        var hasCloudAttributeEvidence = (attributes & CloudStatusFormatter.CloudEvidence) != 0;
        var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

        if (syncRoot is null && !hasCloudAttributeEvidence && !isReparsePoint)
        {
            return Task.FromResult(FileCloudDiagnosticsDetails.None);
        }

        var placeholderState = isReparsePoint
            ? TryGetPlaceholderState(path, attributes)
            : CloudStatusFormatter.PlaceholderStateNone;
        var status = CloudStatusFormatter.Format(attributes, placeholderState);

        var syncRootPath = string.Empty;
        var syncRootId = string.Empty;
        var providerId = string.Empty;
        var provider = string.Empty;
        if (syncRoot is { } matchedSyncRoot)
        {
            syncRootPath = matchedSyncRoot.Path;
            syncRootId = matchedSyncRoot.Id;
            providerId = matchedSyncRoot.ProviderId;
            provider = matchedSyncRoot.DisplayName;
        }

        var isCloudControlled =
            !string.IsNullOrWhiteSpace(syncRootId)
            || !string.IsNullOrWhiteSpace(provider)
            || placeholderState != CloudStatusFormatter.PlaceholderStateNone
            || hasCloudAttributeEvidence;

        var details = isCloudControlled
            ? new FileCloudDiagnosticsDetails(
                true,
                status,
                provider,
                syncRootPath,
                syncRootId,
                providerId,
                (attributes & CloudStatusFormatter.Pinned) != 0,
                (attributes & CloudStatusFormatter.Unpinned) != 0,
                (attributes & CloudStatusFormatter.RecallOnOpen) != 0,
                (attributes & CloudStatusFormatter.RecallOnDataAccess) != 0,
                (attributes & FileAttributes.Offline) != 0)
            : FileCloudDiagnosticsDetails.None;

        return Task.FromResult(details);
    }

    protected override FileCloudDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileCloudDiagnosticsDetails.None;

    /// <summary>
    /// Maps the file's reparse tag + attributes to a CldApi placeholder-state bitmask, or
    /// <see cref="CloudStatusFormatter.PlaceholderStateNone"/> when no reparse tag is present.
    /// </summary>
    private uint TryGetPlaceholderState(string path, FileAttributes attributes)
    {
        using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, attributes.HasFlag(FileAttributes.Directory));
        if (!_fileSystemMetadataInterop.TryGetFileAttributeReparseTag(handle, out var reparseTag))
        {
            return CloudStatusFormatter.PlaceholderStateNone;
        }

        return _cloudFilesInterop.GetPlaceholderState((uint)attributes, reparseTag);
    }
}
