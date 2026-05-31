using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive command intent to flip the inspector pane's visibility (keyboard shortcut). Sent by the
/// Presentation input layer; the shell resolves the new state and broadcasts a
/// <see cref="ToggleInspectorRequestedMessage"/>.
/// </summary>
public sealed record ToggleInspectorKeyPressedMessage : IFileManagerMessengerMessage;
