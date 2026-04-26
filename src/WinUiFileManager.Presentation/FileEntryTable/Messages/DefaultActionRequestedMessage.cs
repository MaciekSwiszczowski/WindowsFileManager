using WinUiFileManager.Presentation.FileEntryTable;


namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);
