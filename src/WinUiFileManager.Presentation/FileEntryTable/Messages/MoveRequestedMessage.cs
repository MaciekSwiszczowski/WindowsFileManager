using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record MoveRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
