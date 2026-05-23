using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public interface IFileEntryDataReader
{
    IReadOnlyList<SpecFileEntryViewModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken);

    SpecFileEntryViewModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken);
}
