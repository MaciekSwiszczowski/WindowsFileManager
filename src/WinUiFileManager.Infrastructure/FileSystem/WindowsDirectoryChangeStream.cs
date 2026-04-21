using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Infrastructure.FileSystem;

/// <summary>
/// Produces a cold observable of <see cref="DirectoryChange"/> events for a directory,
/// backed by <see cref="FileSystemWatcher"/>. When the underlying watcher fails (buffer
/// overflow, removed drive, etc.) the stream emits a single <see cref="DirectoryChangeKind.Invalidated"/>
/// event and silently attempts to recreate the watcher. Consumers are expected to rescan
/// the directory on Invalidated and, if the directory itself disappeared, fall back to the
/// nearest existing ancestor.
/// </summary>
internal sealed class WindowsDirectoryChangeStream : IDirectoryChangeStream
{
    private readonly ILogger<WindowsDirectoryChangeStream> _logger;

    public WindowsDirectoryChangeStream(ILogger<WindowsDirectoryChangeStream> logger)
    {
        _logger = logger;
    }

    public IObservable<DirectoryChange> Watch(NormalizedPath path)
    {
        return Observable.Create<DirectoryChange>(observer =>
        {
            var subscription = new DirectoryWatcherSubscription(path.DisplayPath, _logger, observer);
            subscription.Start();
            return Disposable.Create(subscription, static s => s.Dispose());
        });
    }

    private sealed class DirectoryWatcherSubscription : IDisposable
    {
        private readonly string _path;
        private readonly ILogger _logger;
        private readonly IObserver<DirectoryChange> _observer;
        private readonly object _gate = new();
        private FileSystemWatcher? _watcher;
        private bool _disposed;

        public DirectoryWatcherSubscription(string path, ILogger logger, IObserver<DirectoryChange> observer)
        {
            _path = path;
            _logger = logger;
            _observer = observer;
        }

        public void Start()
        {
            CreateAndStart();
        }

        public void Dispose()
        {
            FileSystemWatcher? toDispose;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                toDispose = _watcher;
                _watcher = null;
            }

            toDispose?.Dispose();
        }

        private void CreateAndStart()
        {
            if (!Directory.Exists(_path))
            {
                _logger.LogDebug("Skipping directory watcher for missing path: {Path}", _path);
                EmitInvalidated();
                return;
            }

            FileSystemWatcher? created = null;

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

                lock (_gate)
                {
                    if (_disposed)
                    {
                        created.Dispose();
                        return;
                    }

                    _watcher = created;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create directory watcher for {Path}", _path);
                created?.Dispose();
                EmitInvalidated();
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
            FileSystemWatcher? toDispose;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _logger.LogWarning(
                    e.GetException(),
                    "Directory watcher failed for {Path}. Emitting Invalidated and recreating watcher.",
                    _path);

                toDispose = _watcher;
                _watcher = null;
            }

            toDispose?.Dispose();
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
