using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent to open the Windows properties dialog for the current selection (typically
/// 'Alt+Enter'). Sent by the Presentation input layer; the active pane behavior resolves it into a
/// <see cref="RequestMessages.PropertiesRequestedMessage"/>.
/// </summary>
public sealed record PropertiesKeyPressedMessage : IFileManagerMessengerMessage;
