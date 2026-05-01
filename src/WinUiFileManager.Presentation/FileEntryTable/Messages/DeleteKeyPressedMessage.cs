namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Command intent message triggered by 'F8' or 'Del'.
/// Consumed by the coordinator to initiate a delete operation.
/// </summary>
public sealed record DeleteKeyPressedMessage;
