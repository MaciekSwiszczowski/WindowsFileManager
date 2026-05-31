using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// Abstraction for reading a single filesystem entry into a row view model, used to refresh one row
/// after a filesystem watcher change rather than re-scanning the whole folder.
/// </summary>
public interface IFileEntryRowReader
{
    /// <summary>Reads the entry at <paramref name="path"/> into a row, or returns null when it cannot
    /// be read (e.g. it no longer exists), which the caller interprets as "remove this row".</summary>
    SpecFileEntryViewModel? TryRead(NormalizedPath path, CancellationToken cancellationToken);
}
