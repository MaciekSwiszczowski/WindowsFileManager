using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Command intent message triggered by 'F7'.
/// </summary>
public sealed record CreateFolderKeyPressedMessage : IFileManagerMessengerMessage;
