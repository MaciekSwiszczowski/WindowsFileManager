namespace WinUiFileManager.Domain.Enums;

public enum DirectoryChangeKind
{
    Created,
    Deleted,
    Changed,
    Renamed,

    // Emitted when the watcher cannot be trusted to deliver incremental events
    // (e.g. FileSystemWatcher buffer overflow, or the watched folder disappeared).
    // Consumers must treat this as "I do not know what happened; do a full rescan
    // and fall back to an existing ancestor if the watched folder is gone".
    Invalidated,
}
