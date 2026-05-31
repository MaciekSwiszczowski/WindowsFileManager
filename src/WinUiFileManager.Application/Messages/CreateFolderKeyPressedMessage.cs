using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent triggered by 'F7'. Sent by the Presentation input layer; the active pane
/// behavior resolves it (source pane) into a <see cref="RequestMessages.CreateFolderRequestedMessage"/>.
/// </summary>
public sealed record CreateFolderKeyPressedMessage : IFileManagerMessengerMessage;
