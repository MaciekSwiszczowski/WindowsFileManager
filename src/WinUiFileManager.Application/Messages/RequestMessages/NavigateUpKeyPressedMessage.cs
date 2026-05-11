using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Primitive keyboard intent for navigating the active pane to its parent directory.
/// </summary>
public sealed record NavigateUpKeyPressedMessage : IFileManagerMessengerMessage;
