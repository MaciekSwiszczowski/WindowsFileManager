using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Command intent message triggered by 'F5'.
/// </summary>
public sealed record CopyKeyPressedMessage : IFileManagerMessengerMessage;
