using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record RenameRequestedMessage(
    string SourceIdentity,
    FileEntryViewModel Item);
