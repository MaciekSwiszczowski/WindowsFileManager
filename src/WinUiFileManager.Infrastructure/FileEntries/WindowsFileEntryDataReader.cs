using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Infrastructure.FileEntries;

internal sealed class WindowsFileEntryDataReader : IFileEntryDataReader
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    public IObservable<FileSystemEntryModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken) =>
        Observable.Create<FileSystemEntryModel>(observer =>
        {
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var subject = new Subject<FileSystemEntryModel>();
            var subscription = subject.Subscribe(observer);

            _ = Task.Run(() => EnumerateEntries(path, subject, cancellation.Token), CancellationToken.None);

            return Disposable.Create(() =>
            {
                cancellation.Cancel();
                subscription.Dispose();
                subject.Dispose();
                cancellation.Dispose();
            });
        });

    public FileSystemEntryModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken)
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

        return fsi is null
            ? null
            : BuildEntryModel(fsi);
    }

    private static FileSystemEntryModel BuildEntryModel(FileSystemInfo fsi)
    {
        var isDirectory = fsi is DirectoryInfo;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        long? size = fsi is FileInfo fi ? fi.Length : null;
        var extension = isDirectory ? string.Empty : fsi.Extension;
        var parentPath = Path.GetDirectoryName(fsi.FullName);

        return new FileSystemEntryModel(
            DirectoryPath.FromFullyQualifiedPath(string.IsNullOrWhiteSpace(parentPath) ? fsi.FullName : parentPath),
            fsi.Name,
            InternExtension(extension),
            kind,
            size,
            ToLocalTime(fsi.LastWriteTimeUtc),
            ToLocalTime(fsi.CreationTimeUtc),
            fsi.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(
        DirectoryPath directoryPath,
        ref FileSystemEntry entry)
    {
        var isDirectory = entry.IsDirectory;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var name = entry.FileName.ToString();
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);

        return new FileSystemEntryModel(
            directoryPath,
            name,
            InternExtension(extension),
            kind,
            isDirectory ? null : entry.Length,
            entry.LastWriteTimeUtc.ToLocalTime(),
            entry.CreationTimeUtc.ToLocalTime(),
            entry.Attributes);
    }

    private static void EnumerateEntries(
        NormalizedPath path,
        IObserver<FileSystemEntryModel> observer,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(path.DisplayPath))
            {
                observer.OnCompleted();
                return;
            }

            var directoryPath = DirectoryPath.FromNormalizedPath(path);
            foreach (var entry in CreateDirectoryEnumerable(directoryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                observer.OnNext(entry);
            }

            observer.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            observer.OnCompleted();
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
        }
    }

    private static DateTimeOffset ToLocalTime(DateTime utcDateTime) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)).ToLocalTime();

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(
        DirectoryPath directoryPath)
    {
        return new FileSystemEnumerable<FileSystemEntryModel>(
            directoryPath.DisplayPath,
            (ref entry) => BuildEntryModel(directoryPath, ref entry),
            EnumerationOptions);
    }

    private static string InternExtension(string extension) =>
        string.IsNullOrEmpty(extension)
            ? string.Empty
            : ExtensionPool.GetOrAdd(extension, static value => value);
}
