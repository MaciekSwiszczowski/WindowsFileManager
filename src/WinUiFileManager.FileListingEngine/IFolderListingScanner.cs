using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// Abstraction for enumerating a folder into the row view models that seed a
/// <see cref="FileListingDataSource"/>'s keyed row store.
/// </summary>
public interface IFolderListingScanner
{
    /// <summary>Scans <paramref name="path"/> and returns its rows (including the synthetic ".." row
    /// where applicable). Implementations should honour <paramref name="cancellationToken"/>.</summary>
    public IReadOnlyList<FileListingRow> Scan(NormalizedPath path, CancellationToken cancellationToken);
}
