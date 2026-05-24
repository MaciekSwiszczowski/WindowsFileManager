using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Table-to-data-source message: apply a sort column and direction for a panel's file entries.
/// Consumed by the active <see cref="WinUiFileManager.Presentation.FileEntryTableData.FileEntryTableDataSource"/> for the matching table identity.
/// </summary>
/// <param name="Identity">Table instance id; must match <see cref="SpecFileEntryTableView.Identity"/>.</param>
/// <param name="Column">Logical file-entry column to sort by.</param>
/// <param name="Ascending">True for ascending order; false for descending order.</param>
public sealed record FileTableSortRequestedMessage(Identity Identity, SortColumn Column, bool Ascending) : IIdentityMessage;
