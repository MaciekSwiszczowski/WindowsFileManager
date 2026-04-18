using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileSystemService
{
    Task<IReadOnlyList<FileSystemEntryModel>> EnumerateDirectoryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken);

    IAsyncEnumerable<IReadOnlyList<FileSystemEntryModel>> EnumerateDirectoryBatchesAsync(
        NormalizedPath path,
        int initialBatchSize,
        int batchSize,
        CancellationToken cancellationToken);

    Task<FileSystemEntryModel?> GetEntryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(NormalizedPath path, CancellationToken cancellationToken);

    Task<bool> DirectoryExistsAsync(NormalizedPath path, CancellationToken cancellationToken);
}
