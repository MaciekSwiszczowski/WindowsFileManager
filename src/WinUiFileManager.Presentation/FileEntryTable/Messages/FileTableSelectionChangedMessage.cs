using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record FileTableSelectionChangedMessage(
    string Identity,
    IReadOnlyList<SpecFileEntryViewModel> SelectedItems,
    bool IsParentRowSelected);
