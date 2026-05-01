namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Primitive intent message triggered by 'Backspace'.
/// Consumed by the coordinator to initiate navigation to the parent directory.
/// </summary>
public sealed record NavigateUpKeyPressedMessage;
