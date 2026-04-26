namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record CopyPathRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileEntryViewModel> Items);
