using System.Reactive.Disposables;
using System.Reactive.Subjects;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.TestApp;

internal sealed class FolderWatchService : IDisposable
{
    private readonly Subject<DirectoryChange> _changes = new();
    private readonly Lock _gate = new();
    private readonly SerialDisposable _watcher = new();
    private bool _disposed;
    private bool _started;

    public FolderWatchService(NormalizedPath path)
    {
        Path = path;
    }

    public NormalizedPath Path { get; }

    public IObservable<DirectoryChange> Changes => _changes;

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_started)
            {
                return;
            }

            _started = true;
        }

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
            _changes.Dispose();
        }
    }

    private void CreateAndStart()
    {
        if (!Directory.Exists(Path.DisplayPath))
        {
            EmitInvalidated();
            return;
        }

        FileSystemWatcher created;

        try
        {
            created = new FileSystemWatcher(Path.DisplayPath)
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

        _changes.OnNext(new DirectoryChange(
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

        _changes.OnNext(new DirectoryChange(kind, NormalizedPath.FromUserInput(fullPath)));
    }

    private void EmitInvalidated()
    {
        if (IsDisposed())
        {
            return;
        }

        _changes.OnNext(new DirectoryChange(
            DirectoryChangeKind.Invalidated,
            NormalizedPath.FromUserInput(Path.DisplayPath)));
    }

    private bool IsDisposed()
    {
        lock (_gate)
        {
            return _disposed;
        }
    }
}
