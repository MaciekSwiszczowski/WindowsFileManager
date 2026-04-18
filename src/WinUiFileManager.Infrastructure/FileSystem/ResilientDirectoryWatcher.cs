using Microsoft.Extensions.Logging;

namespace WinUiFileManager.Infrastructure.FileSystem;

// FileSystemWatcher stops delivering notifications when its internal buffer
// overflows under bursty directory activity and surfaces that failure through
// the Error event. This wrapper handles that event by disposing the faulted
// watcher, creating a fresh one, and raising one synthetic change callback so
// the caller rescans the directory and does not miss events that happened
// during the gap.
//
// The recovery path is intentionally not covered by a dedicated unit test:
// forcing a real buffer overflow is inherently racy. The pane-level
// "directory disappeared" tests cover the rescan/fallback behavior callers
// rely on, and the happy-path subscription is covered by the existing
// WindowsFileSystemService watch test.
internal sealed class ResilientDirectoryWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly Action _onChanged;
    private readonly ILogger _logger;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public ResilientDirectoryWatcher(string path, Action onChanged, ILogger logger)
    {
        _path = path;
        _onChanged = onChanged;
        _logger = logger;

        CreateAndStartWatcher();
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

    private void CreateAndStartWatcher()
    {
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

            created.Created += OnFileSystemChanged;
            created.Deleted += OnFileSystemChanged;
            created.Changed += OnFileSystemChanged;
            created.Renamed += OnFileSystemRenamed;
            created.Error += OnFileSystemError;
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
        }
    }

    private void OnFileSystemChanged(object? sender, FileSystemEventArgs e)
    {
        RaiseChangedIfActive();
    }

    private void OnFileSystemRenamed(object? sender, RenamedEventArgs e)
    {
        RaiseChangedIfActive();
    }

    private void OnFileSystemError(object? sender, ErrorEventArgs e)
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
                "Directory watcher failed for {Path}. Recreating watcher and requesting rescan.",
                _path);

            toDispose = _watcher;
            _watcher = null;
        }

        toDispose?.Dispose();
        CreateAndStartWatcher();
        _onChanged();
    }

    private void RaiseChangedIfActive()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        _onChanged();
    }
}
