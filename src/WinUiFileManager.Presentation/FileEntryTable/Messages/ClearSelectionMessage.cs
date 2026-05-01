namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Primitive intent message triggered by 'Ctrl+Shift+A' or 'Esc'.
/// Empties SelectedItems and clears '..''s visual highlight.
/// </summary>
public sealed record ClearSelectionMessage;
