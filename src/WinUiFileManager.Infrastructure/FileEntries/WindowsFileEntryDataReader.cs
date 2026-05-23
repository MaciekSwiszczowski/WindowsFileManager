using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Infrastructure.FileEntries;

internal sealed class WindowsFileEntryDataReader : IFileEntryDataReader
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool = new(StringComparer.OrdinalIgnoreCase);

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

        if (File.Exists(displayPath))
        {
            return BuildEntryModel(new FileInfo(displayPath));
        }

        if (Directory.Exists(displayPath))
        {
            return BuildEntryModel(new DirectoryInfo(displayPath));
        }

        return null;
    }

    private static FileSystemEntryModel BuildEntryModel(FileInfo fileInfo)
    {
        var parentPath = Path.GetDirectoryName(fileInfo.FullName);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(string.IsNullOrWhiteSpace(parentPath) ? fileInfo.FullName : parentPath),
            fileInfo.Name,
            InternExtension(fileInfo.Extension),
            ItemKind.File,
            fileInfo.Length,
            fileInfo.LastWriteTime,
            fileInfo.CreationTime,
            fileInfo.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(DirectoryInfo directoryInfo)
    {
        var parentPath = Path.GetDirectoryName(directoryInfo.FullName);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(string.IsNullOrWhiteSpace(parentPath) ? directoryInfo.FullName : parentPath),
            directoryInfo.Name,
            InternExtension(string.Empty),
            ItemKind.Directory,
            null,
            directoryInfo.LastWriteTime,
            directoryInfo.CreationTime,
            directoryInfo.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(NormalizedPath directoryPath, ref FileSystemEntry entry)
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
            entry.LastWriteTimeUtc.ToLocalTime().DateTime,
            entry.CreationTimeUtc.ToLocalTime().DateTime,
            entry.Attributes);
    }

    private static void EnumerateEntries(NormalizedPath path, IObserver<FileSystemEntryModel> observer, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(path.DisplayPath))
            {
                observer.OnCompleted();
                return;
            }

            var directoryPath = path;
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

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(NormalizedPath directoryPath)
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
