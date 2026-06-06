using System.IO.Enumeration;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.FileListingEngine;

namespace WinUiFileManager.FileListingEngine.FileSystem;

/// <summary>
/// Default <see cref="IFolderListingScanner"/>: enumerates a directory with
/// <see cref="FileSystemEnumerable{TResult}"/>, prepends the synthetic ".." parent row when the folder
/// has a parent, and projects each native entry into a row via <see cref="FileListingRowFactory"/>.
/// </summary>
/// <remarks>
/// <see cref="FileSystemEnumerable{TResult}"/> is used (rather than <c>Directory.GetFiles</c>) so each
/// row is built directly from the native enumeration record without an extra stat per file. Inaccessible
/// entries are skipped (<see cref="EnumerationOptions.IgnoreInaccessible"/>) and the cancellation token
/// is honoured between entries so a folder change can abandon a large scan promptly.
/// </remarks>
internal sealed class WindowsFolderListingScanner : IFolderListingScanner
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    private readonly FileListingRowFactory _rowFactory;

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rowFactory"/> is null.</exception>
    public WindowsFolderListingScanner(FileListingRowFactory rowFactory)
    {
        ArgumentNullException.ThrowIfNull(rowFactory);
        _rowFactory = rowFactory;
    }

    /// <summary>Scans <paramref name="path"/> into rows. Returns empty when the directory does not
    /// exist. The ".." row is included only when the folder has a parent (i.e. not a drive root).</summary>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is
    /// signalled before or during enumeration.</exception>
    public IReadOnlyList<FileListingRow> Scan(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        if (!Directory.Exists(displayPath))
        {
            return [];
        }

        var entries = new List<FileListingRow>();
        if (Directory.GetParent(displayPath) is not null)
        {
            entries.Add(FileListingRow.CreateParentEntry());
        }

        foreach (var entry in EnumerateEntries(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>Lazily enumerates the directory, transforming each native entry into a row via the
    /// factory directly from the enumeration record (no second filesystem hit per entry).</summary>
    private FileSystemEnumerable<FileListingRow> EnumerateEntries(NormalizedPath directoryPath)
    {
        return new FileSystemEnumerable<FileListingRow>(
            directoryPath.DisplayPath,
            (ref entry) => _rowFactory.Create(directoryPath, ref entry),
            EnumerationOptions);
    }
}
