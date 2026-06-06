using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Outgoing control message published whenever the selection or active item changes.
/// </summary>
/// <param name="Identity">The table identity that produced the selection update.</param>
/// <param name="SelectedItems">
/// Selected real file-system rows. The synthetic parent row is never included here because it is not a command target.
/// </param>
/// <param name="IsParentRowSelected">
/// Whether the synthetic parent row is visually selected. Kept separate from <paramref name="SelectedItems" /> because
/// the parent row has no file-system model and must not participate in file commands.
/// </param>
/// <param name="ActiveItem">
/// The current navigation/activation row. This may differ from <paramref name="SelectedItems" /> during range selection
/// or parent-row navigation, and is used by table UI behaviors such as the active-row indicator.
/// </param>
public sealed record FileTableSelectionChangedMessage(
    Identity Identity,
    IReadOnlyList<FileListingRow> SelectedItems,
    bool IsParentRowSelected,
    FileListingRow? ActiveItem = null) : IIdentityMessage;
