using WinUiFileManager.Presentation.FileEntryTable;


namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record PropertiesRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);
