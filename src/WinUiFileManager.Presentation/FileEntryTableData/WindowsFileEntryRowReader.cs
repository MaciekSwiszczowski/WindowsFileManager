using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// Default <see cref="IFileEntryRowReader"/>: reads a single filesystem entry on demand (used to refresh
/// one row in response to a watcher change) and builds a row via <see cref="FileEntryRowFactory"/>.
/// </summary>
/// <remarks>
/// Any read failure other than cancellation is swallowed and surfaced as a null result, which the data
/// source treats as "the entry is gone, remove its row" — watcher notifications routinely race the
/// filesystem (e.g. a temp file created and deleted before we read it).
/// </remarks>
internal sealed class WindowsFileEntryRowReader : IFileEntryRowReader
{
    private readonly FileEntryRowFactory _rowFactory;

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rowFactory"/> is null.</exception>
    public WindowsFileEntryRowReader(FileEntryRowFactory rowFactory)
    {
        ArgumentNullException.ThrowIfNull(rowFactory);
        _rowFactory = rowFactory;
    }

    /// <summary>Reads the entry at <paramref name="path"/> into a row, or returns null if it cannot be
    /// read (missing/inaccessible). Cancellation propagates; all other exceptions yield null.</summary>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is
    /// signalled.</exception>
    public SpecFileEntryViewModel? TryRead(NormalizedPath path, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var displayPath = path.DisplayPath;
            var attributes = File.GetAttributes(displayPath);
            var directoryPath = NormalizedPath.FromFullyQualifiedPath(Path.GetDirectoryName(displayPath) ?? displayPath);

            return attributes.HasFlag(FileAttributes.Directory)
                ? _rowFactory.Create(directoryPath, new DirectoryInfo(displayPath))
                : _rowFactory.Create(directoryPath, new FileInfo(displayPath));
        }
        // Swallow read failures (file vanished, locked, access denied) but never hide a cancellation.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
