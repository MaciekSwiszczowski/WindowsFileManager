using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileIdentityService
{
    Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken);

    Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken);
}
