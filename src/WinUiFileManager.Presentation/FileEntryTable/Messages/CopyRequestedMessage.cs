using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record CopyRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileEntryViewModel> Items);
