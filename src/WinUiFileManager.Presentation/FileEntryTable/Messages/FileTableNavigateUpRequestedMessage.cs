namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Outgoing control message published when navigation up is requested (e.g., via 'Backspace' or double-click on '..').
/// </summary>
public sealed record FileTableNavigateUpRequestedMessage(string Identity);
