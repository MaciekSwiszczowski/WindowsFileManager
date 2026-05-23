using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class WindowsFileEntryRowReader : IFileEntryRowReader
{
    private readonly FileEntryRowFactory _rowFactory;

    public WindowsFileEntryRowReader(FileEntryRowFactory rowFactory)
    {
        ArgumentNullException.ThrowIfNull(rowFactory);
        _rowFactory = rowFactory;
    }

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
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
