namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record MoveRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileEntryViewModel> Items);
