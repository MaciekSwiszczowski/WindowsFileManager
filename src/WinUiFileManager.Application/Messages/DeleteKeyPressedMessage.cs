using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent triggered by 'F8' or 'Del'. Sent by the Presentation input layer; the
/// active pane behavior resolves it (source pane + selection) into a
/// <see cref="RequestMessages.DeleteRequestedMessage"/>.
/// </summary>
public sealed record DeleteKeyPressedMessage : IFileManagerMessengerMessage;
