namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Outgoing control message published when the table receives focus.
/// Used by the parent view to manage focus state between multiple tables.
/// </summary>
public sealed record FileTableFocusedMessage(string Identity);
