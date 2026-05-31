using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Resolved request carrying the desired inspector visibility. Sent by the command-buttons view model
/// (and the shell in response to <see cref="ToggleInspectorKeyPressedMessage"/>); handled by
/// <c>MainShellViewModel</c>, <c>InspectorViewModel</c>, and <c>CommandButtonsViewModel</c> to update layout/state.
/// </summary>
/// <param name="IsVisible">The target inspector visibility.</param>
public sealed record ToggleInspectorRequestedMessage(bool IsVisible) : IFileManagerMessengerMessage;
