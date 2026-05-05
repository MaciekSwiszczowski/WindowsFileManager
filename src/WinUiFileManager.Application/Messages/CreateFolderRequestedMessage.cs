namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request to create a folder in a pane.
/// </summary>
public sealed record CreateFolderRequestedMessage(string SourceIdentity);
