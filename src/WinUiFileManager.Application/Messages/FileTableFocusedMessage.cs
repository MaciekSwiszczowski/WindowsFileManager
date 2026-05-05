namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Published when a file table receives or loses focus.
/// </summary>
public sealed record FileTableFocusedMessage(string Identity, bool IsFocused = true);
