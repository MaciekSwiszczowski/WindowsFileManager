using Microsoft.Win32.SafeHandles;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

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

    public void Initialize()
    {
        _messenger.Register<InspectorIdentityDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
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

    private static DateTime FromFileTimeUtc(long fileTime) =>
        fileTime <= 0 ? DateTime.MinValue : DateTime.FromFileTimeUtc(fileTime);
}
