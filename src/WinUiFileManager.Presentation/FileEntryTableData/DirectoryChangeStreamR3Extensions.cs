using R3;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// R3 adapter for the existing directory-change observable contract.
/// </summary>
/// <remarks>
/// This keeps the phase-two table data-source port from spreading direct System.Reactive interop calls
/// through the file display engine. Removing <see cref="IDirectoryChangeStream"/>'s Rx-shaped contract can
/// be a later application-layer cleanup.
/// </remarks>
internal static class DirectoryChangeStreamR3Extensions
{
    /// <summary>Converts one cold directory watcher observable into R3 while preserving subscription ownership.</summary>
    public static Observable<DirectoryChange> WatchR3(this IDirectoryChangeStream directoryChangeStream, NormalizedPath path)
    {
        return directoryChangeStream.Watch(path).ToObservable();
    }
}
