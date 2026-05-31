using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Primitive keyboard intent to navigate the active pane to its parent directory. Sent by the
/// Presentation input layer; the active pane behavior resolves it (which pane) into a
/// <see cref="NavigateUpRequestedMessage"/>.
/// </summary>
public sealed record NavigateUpKeyPressedMessage : IFileManagerMessengerMessage;
