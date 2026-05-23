using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTableData;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileEntryRowReader : IFileEntryRowReader
{
    public SpecFileEntryViewModel? TryRead(NormalizedPath path, CancellationToken cancellationToken) => null;
}
