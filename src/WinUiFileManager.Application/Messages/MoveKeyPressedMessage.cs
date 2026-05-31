using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent triggered by 'F6'. Sent by the Presentation input layer; the active pane
/// behavior resolves it (source/target panes + selection) into a
/// <see cref="RequestMessages.MoveRequestedMessage"/>.
/// </summary>
public sealed record MoveKeyPressedMessage : IFileManagerMessengerMessage;
