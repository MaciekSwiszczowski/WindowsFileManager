using System.Reactive.Concurrency;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileSystemService
{
    Task<IReadOnlyList<FileSystemEntryModel>> EnumerateDirectoryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken);

    /// <summary>
    /// Emits each file-system entry of the directory as a cold observable. Enumeration is
    /// scheduled on <paramref name="scheduler"/>, so callers can keep the work off the UI
    /// thread and compose their own buffering/throttling/observe-on pipelines on top.
    /// </summary>
    IObservable<FileSystemEntryModel> ObserveDirectoryEntries(
        NormalizedPath path,
        IScheduler scheduler,
        CancellationToken cancellationToken);

    Task<FileSystemEntryModel?> GetEntryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(NormalizedPath path, CancellationToken cancellationToken);

    Task<bool> DirectoryExistsAsync(NormalizedPath path, CancellationToken cancellationToken);
}
