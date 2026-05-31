using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Primitive intent message triggered by 'Enter' or mouse double-click on a real row. Sent by the
/// Presentation input layer (keyboard manager / row interaction) and resolved by the active pane
/// behavior into a <see cref="RequestMessages.DefaultActionRequestedMessage"/> for the focused selection.
/// </summary>
public sealed record ActivateInvokedMessage : IFileManagerMessengerMessage;
