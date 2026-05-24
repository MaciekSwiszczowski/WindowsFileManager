using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Host-to-table message: apply column pixel widths for the file entry grid.
/// Consumed by <see cref="FileEntryTableLayoutBehavior"/> on the target <see cref="SpecFileEntryTableView"/>.
/// </summary>
/// <param name="Identity">Table instance id; must match <see cref="SpecFileEntryTableView.Identity"/>.</param>
/// <param name="Layout">Width of each logical column (Name, Extension, Size, Modified, Attributes).</param>
public sealed record FileTableColumnLayoutMessage(Identity Identity, ColumnLayout Layout) : IIdentityMessage;
