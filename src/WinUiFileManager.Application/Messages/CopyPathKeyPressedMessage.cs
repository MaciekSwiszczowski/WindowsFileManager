using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent triggered by 'Ctrl+Shift+C'. Sent by the Presentation input layer; the
/// active pane behavior resolves it (source pane + selection) into a
/// <see cref="RequestMessages.CopyPathRequestedMessage"/> which copies the selected paths to the clipboard.
/// </summary>
public sealed record CopyPathKeyPressedMessage : IFileManagerMessengerMessage;
