using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to perform the default action on an item: open a file with its default app or enter
/// a directory. Produced by the pane behavior from an
/// <see cref="WinUiFileManager.Application.Messages.ActivateInvokedMessage"/>; handled by navigation/shell code.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the action originated in.</param>
/// <param name="Item">The activated entry (file to open or directory to enter).</param>
public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item) : IFileManagerMessengerMessage;
