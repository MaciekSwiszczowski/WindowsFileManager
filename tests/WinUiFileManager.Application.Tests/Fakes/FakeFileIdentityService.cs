namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileIdentityService : IFileIdentityService
{
    public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileNtfsMetadataDetails> GetNtfsMetadataDetailsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> SetNtfsAttributeFlagAsync(
        string path,
        FileAttributes flag,
        bool enabled,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<FileCloudDiagnosticsDetails> GetCloudDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
