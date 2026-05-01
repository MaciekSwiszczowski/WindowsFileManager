namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Outgoing control message published whenever the selection or active item changes.
/// </summary>
public sealed record FileTableSelectionChangedMessage(
    string Identity,
    IReadOnlyList<SpecFileEntryViewModel> SelectedItems,
    bool IsParentRowSelected,
    SpecFileEntryViewModel? ActiveItem = null);
