using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Published when a file table receives or loses focus.
/// </summary>
public sealed record FileTableFocusedMessage(Identity Identity, bool IsFocused = true) : IIdentityMessage;
