using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Emits <see cref="DirectoryChange"/> events for the contents of a directory.
/// The returned observable is cold: each subscription starts and owns its own
/// watcher. Emissions may arrive on a background thread; consumers are
/// expected to buffer and marshal to the UI thread themselves.
/// </summary>
public interface IDirectoryChangeStream
{
    IObservable<DirectoryChange> Watch(NormalizedPath path);
}
