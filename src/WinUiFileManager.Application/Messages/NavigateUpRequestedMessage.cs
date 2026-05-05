namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to navigate a pane to its parent directory.
/// </summary>
public sealed record NavigateUpRequestedMessage(string SourceIdentity);
