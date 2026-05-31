using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// Abstraction for enumerating a folder into the row view models that seed a
/// <see cref="FileEntryTableDataSource"/>'s source cache.
/// </summary>
public interface IFolderEntryScanner
{
    /// <summary>Scans <paramref name="path"/> and returns its rows (including the synthetic ".." row
    /// where applicable). Implementations should honour <paramref name="cancellationToken"/>.</summary>
    IReadOnlyList<SpecFileEntryViewModel> Scan(NormalizedPath path, CancellationToken cancellationToken);
}
