using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Published by a pane view (<c>SinglePanelView</c>) when its file table gains or loses focus, so the
/// active-panel tracking and inspector can follow the focused pane. Identity-scoped to the pane.
/// </summary>
/// <param name="Identity">The pane whose focus changed.</param>
/// <param name="IsFocused"><see langword="true"/> when the table gained focus; <see langword="false"/> when it lost focus.</param>
public sealed record FileTableFocusedMessage(Identity Identity, bool IsFocused = true) : IIdentityMessage;
