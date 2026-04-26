using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    FileEntryViewModel Item);
