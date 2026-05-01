namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Host-to-table message: show or hide the synthetic parent row (<c>..</c>) pinned above real items.
/// Consumed by <see cref="FileEntryTableParentEntryVisibilityBehavior"/> on the target <see cref="SpecFileEntryTableView"/>.
/// </summary>
/// <param name="Identity">Table instance id; must match <see cref="SpecFileEntryTableView.Identity"/>.</param>
/// <param name="ShowParentEntry"><c>true</c> to show <c>..</c>; <c>false</c> at a root or when parent navigation is unavailable.</param>
public sealed record FileTableParentEntryVisibilityMessage(string Identity, bool ShowParentEntry);
