using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Command intent message triggered by 'F6'.
/// </summary>
public sealed record MoveKeyPressedMessage : IFileManagerMessengerMessage;
