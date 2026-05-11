using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Command intent message triggered by 'Ctrl+Shift+C'.
/// </summary>
public sealed record CopyPathKeyPressedMessage : IFileManagerMessengerMessage;
