using System.IO.Enumeration;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.FileSystem;

/// <summary>
/// Enumerates directory contents and maps file-system metadata into entry models.
/// Active-folder change notifications live in <see cref="WindowsDirectoryChangeStream"/>.
/// </summary>
internal sealed class WindowsFileSystemService : IFileSystemService
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    private readonly ILogger<WindowsFileSystemService> _logger;

    public WindowsFileSystemService(
        IPathNormalizationService pathService,
        ILogger<WindowsFileSystemService> logger)
    {
        _ = pathService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileSystemEntryModel>> EnumerateDirectoryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var entries = await ObserveDirectoryEntries(path, Scheduler.Immediate, cancellationToken)
            .ToList()
            .ToTask(cancellationToken);
        return (IReadOnlyList<FileSystemEntryModel>)entries;
    }

    public IObservable<FileSystemEntryModel> ObserveDirectoryEntries(
        NormalizedPath path,
        IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        return Observable.Create<FileSystemEntryModel>(observer =>
        {
            var unsubscribe = new CancellationDisposable();
            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, unsubscribe.Token);

            var scheduled = scheduler.Schedule(() => PumpEntries(path, linked.Token, observer));

            return new CompositeDisposable(scheduled, unsubscribe, linked);
        });
    }

    private void PumpEntries(
        NormalizedPath path,
        CancellationToken cancellationToken,
        IObserver<FileSystemEntryModel> observer)
    {
        try
        {
            if (!Directory.Exists(path.DisplayPath))
            {
                WindowsFileSystemServiceLog.DirectoryDoesNotExist(_logger, path.DisplayPath);
                observer.OnCompleted();
                return;
            }

            foreach (var entry in CreateDirectoryEnumerable(path.DisplayPath))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    observer.OnCompleted();
                    return;
                }

                observer.OnNext(entry);
            }

            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            observer.OnCompleted();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }

    public Task<FileSystemEntryModel?> GetEntryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        FileSystemInfo? fsi = null;

        if (File.Exists(displayPath))
        {
            fsi = new FileInfo(displayPath);
        }
        else if (Directory.Exists(displayPath))
        {
            fsi = new DirectoryInfo(displayPath);
        }

        if (fsi is null)
        {
            return Task.FromResult<FileSystemEntryModel?>(null);
        }

        return Task.FromResult<FileSystemEntryModel?>(BuildEntryModel(fsi));
    }

    public Task<bool> ExistsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var displayPath = path.DisplayPath;
        return Task.FromResult(File.Exists(displayPath) || Directory.Exists(displayPath));
    }

    public Task<bool> DirectoryExistsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(path.DisplayPath));
    }

    private static FileSystemEntryModel BuildEntryModel(FileSystemInfo fsi)
    {
        var isDirectory = fsi is DirectoryInfo;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var size = fsi is FileInfo fi ? fi.Length : 0L;
        var extension = isDirectory ? string.Empty : fsi.Extension;

        return new FileSystemEntryModel(
            NormalizedPath.FromUserInput(fsi.FullName),
            fsi.Name,
            extension,
            kind,
            size,
            fsi.LastWriteTimeUtc,
            fsi.CreationTimeUtc,
            fsi.Attributes,
            NtfsFileId.None);
    }

    private static FileSystemEntryModel BuildEntryModel(ref FileSystemEntry entry)
    {
        var fullPath = entry.ToFullPath();
        var isDirectory = entry.IsDirectory;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var name = entry.FileName.ToString();
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);

        return new FileSystemEntryModel(
            NormalizedPath.FromUserInput(fullPath),
            name,
            extension,
            kind,
            isDirectory ? 0L : entry.Length,
            entry.LastWriteTimeUtc.UtcDateTime,
            entry.CreationTimeUtc.UtcDateTime,
            entry.Attributes,
            NtfsFileId.None);
    }

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(string directoryPath)
    {
        return new FileSystemEnumerable<FileSystemEntryModel>(
            directoryPath,
            static (ref entry) => BuildEntryModel(ref entry),
            EnumerationOptions);
    }
}
