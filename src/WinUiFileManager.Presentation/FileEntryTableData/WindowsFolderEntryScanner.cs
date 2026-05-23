using System.IO.Enumeration;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class WindowsFolderEntryScanner : IFolderEntryScanner
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    private readonly FileEntryRowFactory _rowFactory;

    public WindowsFolderEntryScanner(FileEntryRowFactory rowFactory)
    {
        ArgumentNullException.ThrowIfNull(rowFactory);
        _rowFactory = rowFactory;
    }

    public IReadOnlyList<SpecFileEntryViewModel> Scan(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        if (!Directory.Exists(displayPath))
        {
            return [];
        }

        var entries = new List<SpecFileEntryViewModel>();
        if (Directory.GetParent(displayPath) is not null)
        {
            entries.Add(SpecFileEntryViewModel.CreateParentEntry());
        }

        foreach (var entry in EnumerateEntries(path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(entry);
        }

        return entries;
    }

    private FileSystemEnumerable<SpecFileEntryViewModel> EnumerateEntries(NormalizedPath directoryPath)
    {
        return new FileSystemEnumerable<SpecFileEntryViewModel>(
            directoryPath.DisplayPath,
            (ref entry) => _rowFactory.Create(directoryPath, ref entry),
            EnumerationOptions);
    }
}
