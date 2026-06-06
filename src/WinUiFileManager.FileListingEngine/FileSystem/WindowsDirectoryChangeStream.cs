using Microsoft.Extensions.Logging;
using R3;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.FileListingEngine;

namespace WinUiFileManager.FileListingEngine.FileSystem;

/// <summary>
/// Produces a cold R3 observable of <see cref="DirectoryChange"/> events for a directory,
/// backed by <see cref="FileSystemWatcher"/>. When the underlying watcher fails, the stream
/// attempts to recreate the watcher without asking consumers to rescan. Windows implementation of
/// <see cref="IDirectoryChangeStream"/>.
/// </summary>
/// <remarks>
/// COLD-OBSERVABLE MODEL: <see cref="Watch"/> returns a cold observable — nothing is watched until a consumer
/// subscribes, and EACH subscription gets its OWN <see cref="FileSystemWatcher"/> via a private
/// <see cref="DirectoryWatcherSubscription"/>. There is no sharing/multicasting between subscribers, so two
/// subscriptions to the same path run two independent OS watchers. Disposing the subscription (the
/// <see cref="System.IDisposable"/> returned by <c>Subscribe</c>) is what tears the watcher down — that disposal
/// is the owner of the native watcher's lifetime. The singleton itself owns no watcher; its <see cref="Dispose"/>
/// only flips a guard so no further <see cref="Watch"/> calls succeed.
/// Threading: <see cref="FileSystemWatcher"/> raises events on a thread-pool thread, so events reach the observer
/// off the UI thread; consumers must marshal to the UI thread themselves if needed.
/// </remarks>
internal sealed class WindowsDirectoryChangeStream : IDirectoryChangeStream
{
    private readonly ILogger<WindowsDirectoryChangeStream> _logger;
    private bool _disposed;

    public WindowsDirectoryChangeStream(ILogger<WindowsDirectoryChangeStream> logger)
    {
        _logger = logger;
    }

    /// <summary>Returns a cold observable that begins watching <paramref name="path"/> only when subscribed.</summary>
    /// <param name="path">Directory to watch (non-recursive).</param>
    /// <returns>
    /// A cold <see cref="Observable{T}"/>; each subscription owns an independent watcher and must be disposed to
    /// release it.
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the stream has already been disposed.</exception>
    public Observable<DirectoryChange> Watch(NormalizedPath path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Observable.Create runs this factory per subscriber, so each subscriber gets its own watcher subscription;
        // the returned subscription IS the unsubscribe/dispose handle. The state overload avoids a closure.
        return Observable.Create<DirectoryChange, (string Path, ILogger Logger)>(
            (path.DisplayPath, _logger),
            static (observer, state) =>
            {
                var subscription = new DirectoryWatcherSubscription(state.Path, state.Logger, observer);
                subscription.Start();
                return subscription;
            });
    }

    /// <summary>Marks the stream disposed so subsequent <see cref="Watch"/> calls throw. Owns no native resource itself.</summary>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// One subscriber's private watcher: owns a single <see cref="FileSystemWatcher"/> (swappable on recreation)
    /// and forwards its events to one R3 <see cref="Observer{T}"/>. Disposing this stops and releases the watcher
    /// exactly once.
    /// </summary>
    /// <remarks>
    /// The <see cref="_gate"/> lock guards the disposed flag and the swappable watcher so the OS error callback
    /// (which runs on a pool thread and recreates the watcher) cannot race subscriber disposal. Recreation
    /// deliberately does NOT replay or signal a rescan — it transparently re-arms watching so consumers keep their
    /// existing view.
    /// </remarks>
    private sealed class DirectoryWatcherSubscription : IDisposable
    {
        private readonly string _path;
        private readonly ILogger _logger;
        private readonly Observer<DirectoryChange> _observer;
        private readonly Lock _gate = new();
        // The current watcher; assigning a replacement disposes the previous one.
        private FileSystemWatcher? _watcher;
        private readonly Lock _emitGate = new();
        private bool _disposed;

        public DirectoryWatcherSubscription(string path, ILogger logger, Observer<DirectoryChange> observer)
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
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _watcher?.Dispose();
                _watcher = null;
            }

            lock (_emitGate)
            {
                // Wait for an event handler that entered before disposal to finish publishing.
            }
        }

        private void CreateAndStart()
        {
            if (!Directory.Exists(_path))
            {
                _logger.LogDebug("Skipping directory watcher for missing path: {Path}", _path);
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
                        | NotifyFilters.Attributes
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create directory watcher for {Path}", _path);
                return;
            }

            lock (_gate)
            {
                // The subscriber may have disposed while we were building the watcher off-lock; if so, drop the new
                // watcher immediately rather than leaking it.
                if (_disposed)
                {
                    created.Dispose();
                    return;
                }

                // Swap in the new watcher and dispose any prior one (the recreation case).
                _watcher?.Dispose();
                _watcher = created;
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
            lock (_emitGate)
            {
                if (IsDisposed())
                {
                    return;
                }

                _observer.OnNext(new DirectoryChange(
                    DirectoryChangeKind.Renamed,
                    e.FullPath,
                    e.OldFullPath));
            }
        }

        // FileSystemWatcher raises Error when its internal buffer overflows or the directory becomes inaccessible.
        // Tear down the dead watcher (under the gate) then rebuild it outside the lock so consumers transparently
        // keep receiving events without being told to rescan.
        private void OnError(object? sender, ErrorEventArgs e)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _logger.LogWarning(e.GetException(), "Directory watcher failed for {Path}. Recreating watcher.", _path);
                _watcher?.Dispose(); // dispose the failed watcher before rebuilding.
                _watcher = null;
            }

            // CreateAndStart re-checks _disposed under the gate before publishing the replacement watcher.
            CreateAndStart();
        }

        private void Emit(DirectoryChangeKind kind, string fullPath)
        {
            lock (_emitGate)
            {
                if (IsDisposed())
                {
                    return;
                }

                _observer.OnNext(new DirectoryChange(kind, fullPath));
            }
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
