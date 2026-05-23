using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTableData;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeFileEntryDataReader : IFileEntryDataReader
{
    public IReadOnlyList<SpecFileEntryViewModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken) => [];

    public SpecFileEntryViewModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken) => null;
}
