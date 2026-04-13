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
}
