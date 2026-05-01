namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Command intent message triggered by 'Ctrl+Shift+C'.
/// Consumed by the coordinator to initiate copying item paths to the clipboard.
/// </summary>
public sealed record CopyPathKeyPressedMessage;
