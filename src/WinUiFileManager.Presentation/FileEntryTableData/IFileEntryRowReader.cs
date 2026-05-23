using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public interface IFileEntryRowReader
{
    SpecFileEntryViewModel? TryRead(NormalizedPath path, CancellationToken cancellationToken);
}
