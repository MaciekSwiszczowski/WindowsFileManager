using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Primitive command intent to rename the current selection (typically 'F2'). Sent by the Presentation
/// input layer; the active pane behavior resolves it and drives the rename flow (showing the
/// rename dialog via <see cref="WinUiFileManager.Application.Dialogs.RenameDialogViewModel"/>).
/// </summary>
public sealed record RenameKeyPressedMessage : IFileManagerMessengerMessage;
