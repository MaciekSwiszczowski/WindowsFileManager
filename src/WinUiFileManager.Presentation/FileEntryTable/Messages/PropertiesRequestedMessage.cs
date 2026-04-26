using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record PropertiesRequestedMessage(
    string SourceIdentity,
    FileEntryViewModel Item);
