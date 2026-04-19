using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileIdentityService
{
    Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken);

    Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken);

    Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken);

    Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken);

    Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken);

    Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken);

    Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken);
}
