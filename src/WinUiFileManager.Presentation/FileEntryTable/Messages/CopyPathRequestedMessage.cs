using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record CopyPathRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
