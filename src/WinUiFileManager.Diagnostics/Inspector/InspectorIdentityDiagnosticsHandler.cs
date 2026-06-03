using Microsoft.Win32.SafeHandles;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> with
/// NTFS identity details (file id, volume serial, legacy index, link count, final path) and basic NTFS
/// timestamps/attributes for the requested path.
/// </summary>
/// <remarks>
/// The work uses a <see cref="SafeFileHandle"/> from the interop adapter. Failures degrade to an empty result
/// that preserves the requested final path.
/// </remarks>
public sealed class InspectorIdentityDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        InspectorIdentityDiagnosticsDetails,
        InspectorIdentityDiagnosticsResponseMessage>
{
    private readonly IFileSystemMetadataInterop _fileSystemMetadataInterop;

    public InspectorIdentityDiagnosticsHandler(
        IMessenger messenger,
        IFileSystemMetadataInterop fileSystemMetadataInterop,
        ILogger<InspectorIdentityDiagnosticsHandler> logger)
        : base(messenger, logger)
    {
        _fileSystemMetadataInterop = fileSystemMetadataInterop;
    }

    /// <summary>
    /// Opens the path for metadata read and assembles its NTFS metadata + identity details.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>The loaded details, or a near-empty result (with the final path filled in) on failure.</returns>
    /// <remarks>Runs on a thread-pool thread. Errors are logged and degraded by the base class.</remarks>
    protected override Task<InspectorIdentityDiagnosticsDetails> LoadAsync(
        InspectorDiagnosticsRequestMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = message.Path.DisplayPath;
        using var handle = _fileSystemMetadataInterop.OpenForMetadataRead(path, Directory.Exists(path));

        var ntfsMetadata = LoadNtfsMetadata(handle);
        var identity = LoadIdentity(path, handle);
        return Task.FromResult(new InspectorIdentityDiagnosticsDetails(ntfsMetadata, identity));
    }

    protected override InspectorIdentityDiagnosticsResponseMessage CreateResponse(InspectorIdentityDiagnosticsDetails diagnostics) =>
        new(diagnostics);

    protected override InspectorIdentityDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        InspectorIdentityDiagnosticsDetails.Empty with
        {
            Identity = InspectorIdentityDiagnosticsDetails.Empty.Identity with { FinalPath = request.Path.DisplayPath },
        };

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
