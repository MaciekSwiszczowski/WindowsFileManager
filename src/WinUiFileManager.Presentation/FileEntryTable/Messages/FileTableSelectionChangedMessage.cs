namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record FileTableSelectionChangedMessage(
    string Identity,
    IReadOnlyList<FileEntryViewModel> SelectedItems);
