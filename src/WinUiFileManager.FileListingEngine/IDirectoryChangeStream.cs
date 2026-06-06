using R3;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// Emits <see cref="DirectoryChange"/> events for the contents of a directory.
/// The returned R3 observable is cold: each subscription starts and owns its own
/// watcher. Emissions may arrive on a background thread; consumers are
/// expected to buffer and marshal to the UI thread themselves.
/// </summary>
public interface IDirectoryChangeStream : IDisposable
{
    public Observable<DirectoryChange> Watch(NormalizedPath path);
}
