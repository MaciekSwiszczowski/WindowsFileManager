using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.FileSystem;

public sealed class NtfsFileIdentityService : IFileIdentityService
{
    private readonly IFileIdentityInterop _fileIdentityInterop;

    public NtfsFileIdentityService(IFileIdentityInterop fileIdentityInterop)
    {
        _fileIdentityInterop = fileIdentityInterop;
    }

    public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _fileIdentityInterop.GetFileId(path);

        var fileId = result is { Success: true, FileId128: not null }
            ? new NtfsFileId(result.FileId128)
            : NtfsFileId.None;

        return Task.FromResult(fileId);
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
}
