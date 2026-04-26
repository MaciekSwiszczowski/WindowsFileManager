namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileEntryViewModel> Items);
