using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to create a new folder in the source pane's current directory. Produced by the pane
/// behavior from a <see cref="WinUiFileManager.Application.Messages.CreateFolderKeyPressedMessage"/>; handled by the file-operations layer.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane in which to create the folder.</param>
public sealed record CreateFolderRequestedMessage(string SourceIdentity) : IFileManagerMessengerMessage;
