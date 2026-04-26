using WinUiFileManager.Presentation.FileEntryTable;


namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record RenameRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);
