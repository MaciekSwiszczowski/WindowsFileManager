using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTableData;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFolderEntryScanner : IFolderEntryScanner
{
    public IReadOnlyList<SpecFileEntryViewModel> Scan(NormalizedPath path, CancellationToken cancellationToken) => [];
}
