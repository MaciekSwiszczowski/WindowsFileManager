using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent triggered by 'F5' (or the Copy command button). Sent by the Presentation
/// input layer; the active pane behavior resolves it (source/target panes + selection) into a
/// <see cref="RequestMessages.CopyRequestedMessage"/>.
/// </summary>
public sealed record CopyKeyPressedMessage : IFileManagerMessengerMessage;
