using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public interface IFolderEntryScanner
{
    IReadOnlyList<SpecFileEntryViewModel> Scan(NormalizedPath path, CancellationToken cancellationToken);
}
