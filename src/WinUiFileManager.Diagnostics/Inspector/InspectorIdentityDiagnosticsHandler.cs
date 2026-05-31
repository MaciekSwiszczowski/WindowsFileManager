using Microsoft.Win32.SafeHandles;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorIdentityDiagnosticsRequestMessage"/> with
/// NTFS identity details (file id, volume serial, legacy index, link count, final path) and basic NTFS
/// timestamps/attributes for the requested path.
/// </summary>
/// <remarks>
/// Lifetime: DI singleton; <see cref="Initialize"/> registers with the messenger and <see cref="Dispose"/>
/// unregisters, but the container is never disposed (AGENTS.md §5) so <see cref="Dispose"/> is effectively
/// unreachable — treat as process-lifetime.
/// Threading: the request is answered with <c>message.Reply(Task.Run(...))</c>, so <see cref="Load"/> runs
/// on a thread-pool thread (library convention, AGENTS.md §6). The work uses a <see cref="SafeFileHandle"/>
/// from the interop adapter and is bounded by a <see cref="LoadTimeout"/> linked to the request's token.
/// </remarks>
public sealed class InspectorIdentityDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;
    private readonly ILogger<InspectorIdentityDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorIdentityDiagnosticsHandler(
        IMessenger messenger,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        ILogger<InspectorIdentityDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
        _logger = logger;
    }

    /// <summary>
    /// Registers the request handler, replying with a task that loads the details off-thread.
    /// </summary>
    /// <remarks>Not idempotent — must be called exactly once (AGENTS.md §4).</remarks>
    public void Initialize()
    {
        _messenger.Register<InspectorIdentityDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    /// <summary>Unregisters from the messenger (idempotent). See type remarks: effectively never called.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Opens the path for metadata read and assembles its NTFS metadata + identity details.
    /// </summary>
    /// <param name="message">The request carrying the target path and a cancellation token.</param>
    /// <returns>The loaded details, or a near-empty result (with the final path filled in) on failure.</returns>
    /// <remarks>
    /// Runs on a thread-pool thread. A linked CTS adds the <see cref="LoadTimeout"/> on top of the
    /// request's token. Genuine caller cancellation is rethrown; any other failure is logged and degraded
    /// to an empty result rather than propagated, so the inspector UI shows blanks instead of an error.
    /// </remarks>
    private InspectorIdentityDiagnosticsDetails Load(InspectorIdentityDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var path = message.Path.DisplayPath;
            using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));

            var ntfsMetadata = LoadNtfsMetadata(handle);
            var identity = LoadIdentity(path, handle);
            return new InspectorIdentityDiagnosticsDetails(ntfsMetadata, identity);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Only rethrow for real caller cancellation; a timeout cancellation falls through to the
            // general handler below and degrades to an empty result.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector identity diagnostics for {Path}", message.Path.DisplayPath);
            return InspectorIdentityDiagnosticsDetails.Empty with
            {
                Identity = InspectorIdentityDiagnosticsDetails.Empty.Identity with { FinalPath = message.Path.DisplayPath },
            };
        }
    }

    /// <summary>Reads basic NTFS info (attributes + the four timestamps) from an open handle.</summary>
    /// <exception cref="InvalidOperationException">The underlying GetFileInformationByHandleEx call failed.</exception>
    private FileNtfsMetadataDetails LoadNtfsMetadata(SafeFileHandle handle)
    {
        if (!_fileSystemMetadataInterop.TryGetFileBasicInfo(handle, out var basicInfo))
        {
            throw new InvalidOperationException("GetFileInformationByHandleEx(FileBasicInfo) failed.");
        }

        return new FileNtfsMetadataDetails(
            (FileAttributes)basicInfo.FileAttributes,
            FromFileTimeUtc(basicInfo.CreationTime),
            FromFileTimeUtc(basicInfo.LastAccessTime),
            FromFileTimeUtc(basicInfo.LastWriteTime),
            FromFileTimeUtc(basicInfo.ChangeTime));
    }

    /// <summary>
    /// Builds the file-identity view: 128-bit NTFS file id (falling back to the legacy 64-bit index),
    /// volume serial, link count, and the resolved final path.
    /// </summary>
    /// <param name="path">Display path, used for volume-root and full-path fallbacks.</param>
    /// <param name="handle">Open metadata-read handle to the file.</param>
    private FileIdentityDetails LoadIdentity(string path, SafeFileHandle handle)
    {
        var fileId = _fileSystemMetadataInterop.TryGetNtfsFileIdBytes(handle, out var idBytes) && idBytes is not null
            ? new NtfsFileId(idBytes)
            : NtfsFileId.None;

        var hasLegacy = _fileSystemMetadataInterop.TryGetLegacyFileIndex(handle, out var legacyInfo, out _);
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        var volumeSerial = _fileSystemMetadataInterop.TryGetVolumeSerialHex(root ?? string.Empty);
        var finalPath = _fileSystemMetadataInterop.TryGetFinalPath(handle) ?? Path.GetFullPath(path);

        return new FileIdentityDetails(
            fileId,
            volumeSerial ?? string.Empty,
            hasLegacy
                ? $"0x{((ulong)legacyInfo.FileIndexHigh << 32 | legacyInfo.FileIndexLow):X16}"
                : string.Empty,
            hasLegacy ? legacyInfo.NumberOfLinks.ToString() : string.Empty,
            finalPath);
    }

    // Win32 FILETIME of 0 (or negative) means "not set"; map that to DateTime.MinValue rather than the
    // 1601 epoch FromFileTimeUtc would otherwise produce.
    private static DateTime FromFileTimeUtc(long fileTime) =>
        fileTime <= 0 ? DateTime.MinValue : DateTime.FromFileTimeUtc(fileTime);
}
