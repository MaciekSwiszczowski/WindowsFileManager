using WinUiFileManager.Presentation.FileEntryTable;


namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record CopyRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);
