using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.TestApp;

internal sealed class WindowsFolderEntryStream : IDisposable
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    private readonly CancellationTokenSource _disposeCts = new();

    public WindowsFolderEntryStream(NormalizedPath path, IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        Path = path;
        Entries = CreateEntriesObservable(path, scheduler);
    }

    public NormalizedPath Path { get; }

    public IObservable<FileSystemEntryModel> Entries { get; }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    public IObservable<DirectoryChange> WatchChanges()
    {
        ObjectDisposedException.ThrowIf(_disposeCts.IsCancellationRequested, this);

        return Observable.Create<DirectoryChange>(observer =>
        {
            var subscription = new DirectoryWatcherSubscription(Path.DisplayPath, observer);
            subscription.Start();
            return subscription;
        });
    }

    public static FileSystemEntryModel? GetEntry(NormalizedPath path)
    {
        try
        {
            var displayPath = path.DisplayPath;
            FileSystemInfo? info = null;

            if (File.Exists(displayPath))
            {
                info = new FileInfo(displayPath);
            }
            else if (Directory.Exists(displayPath))
            {
                info = new DirectoryInfo(displayPath);
            }

            return info is null ? null : BuildEntryModel(info);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private IObservable<FileSystemEntryModel> CreateEntriesObservable(
        NormalizedPath path,
        IScheduler scheduler)
    {
        return Observable.Create<FileSystemEntryModel>(observer =>
        {
            var unsubscribe = new CancellationDisposable();
            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _disposeCts.Token, unsubscribe.Token);
            var scheduled = scheduler.Schedule(() => PumpEntries(path, linked.Token, observer));

            return new CompositeDisposable(scheduled, unsubscribe, linked);
        });
    }

    private static void PumpEntries(
        NormalizedPath path,
        CancellationToken cancellationToken,
        IObserver<FileSystemEntryModel> observer)
    {
        try
        {
            if (!Directory.Exists(path.DisplayPath))
            {
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

    private static FileSystemEntryModel BuildEntryModel(ref FileSystemEntry entry)
    {
        var fullPath = entry.ToFullPath();
        var isDirectory = entry.IsDirectory;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var name = entry.FileName.ToString();
        var extension = isDirectory ? string.Empty : System.IO.Path.GetExtension(name);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(fullPath),
            name,
            InternExtension(extension),
            kind,
            isDirectory ? 0L : entry.Length,
            entry.LastWriteTimeUtc.UtcDateTime,
            entry.CreationTimeUtc.UtcDateTime,
            entry.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(FileSystemInfo info)
    {
        var isDirectory = info is DirectoryInfo;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var extension = isDirectory ? string.Empty : info.Extension;

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(info.FullName),
            info.Name,
            InternExtension(extension),
            kind,
            isDirectory ? 0L : ((FileInfo)info).Length,
            info.LastWriteTimeUtc,
            info.CreationTimeUtc,
            info.Attributes);
    }

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(
        string directoryPath)
    {
        return new FileSystemEnumerable<FileSystemEntryModel>(
            directoryPath,
            static (ref entry) => BuildEntryModel(ref entry),
            EnumerationOptions);
    }

    private static string InternExtension(string extension) =>
        string.IsNullOrEmpty(extension)
            ? string.Empty
            : ExtensionPool.GetOrAdd(extension, static value => value);

    private sealed class DirectoryWatcherSubscription : IDisposable
    {
        private readonly string _path;
        private readonly IObserver<DirectoryChange> _observer;
        private readonly Lock _gate = new();
        private readonly SerialDisposable _watcher = new();
        private bool _disposed;

        public DirectoryWatcherSubscription(string path, IObserver<DirectoryChange> observer)
        {
            _path = path;
            _observer = observer;
        }

        public void Start()
        {
            CreateAndStart();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _watcher.Dispose();
            }
        }

        private void CreateAndStart()
        {
            if (!Directory.Exists(_path))
            {
                EmitInvalidated();
                return;
            }

            FileSystemWatcher created;

            try
            {
                created = new FileSystemWatcher(_path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.CreationTime
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                };

                created.Created += OnCreated;
                created.Deleted += OnDeleted;
                created.Changed += OnChanged;
                created.Renamed += OnRenamed;
                created.Error += OnError;
                created.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                EmitInvalidated();
                return;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    created.Dispose();
                    return;
                }

                _watcher.Disposable = Disposable.Create(() =>
                {
                    created.Created -= OnCreated;
                    created.Deleted -= OnDeleted;
                    created.Changed -= OnChanged;
                    created.Renamed -= OnRenamed;
                    created.Error -= OnError;
                    created.Dispose();
                });
            }
        }

        private void OnCreated(object? sender, FileSystemEventArgs e) =>
            Emit(DirectoryChangeKind.Created, e.FullPath);

        private void OnDeleted(object? sender, FileSystemEventArgs e) =>
            Emit(DirectoryChangeKind.Deleted, e.FullPath);

        private void OnChanged(object? sender, FileSystemEventArgs e) =>
            Emit(DirectoryChangeKind.Changed, e.FullPath);

        private void OnRenamed(object? sender, RenamedEventArgs e)
        {
            if (IsDisposed())
            {
                return;
            }

            _observer.OnNext(new DirectoryChange(
                DirectoryChangeKind.Renamed,
                NormalizedPath.FromUserInput(e.FullPath),
                NormalizedPath.FromUserInput(e.OldFullPath)));
        }

        private void OnError(object? sender, ErrorEventArgs e)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _watcher.Disposable = null;
            }

            EmitInvalidated();
            CreateAndStart();
        }

        private void Emit(DirectoryChangeKind kind, string fullPath)
        {
            if (IsDisposed())
            {
                return;
            }

            _observer.OnNext(new DirectoryChange(kind, NormalizedPath.FromUserInput(fullPath)));
        }

        private void EmitInvalidated()
        {
            if (IsDisposed())
            {
                return;
            }

            _observer.OnNext(new DirectoryChange(
                DirectoryChangeKind.Invalidated,
                NormalizedPath.FromUserInput(_path)));
        }

        private bool IsDisposed()
        {
            lock (_gate)
            {
                return _disposed;
            }
        }
    }
}
