using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to delete selected items. Produced by the pane behavior from a
/// <see cref="WinUiFileManager.Application.Messages.DeleteKeyPressedMessage"/>; handled by the file-operations layer.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the items came from.</param>
/// <param name="Items">The entries to delete.</param>
public sealed record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileSystemEntryModel> Items) : IFileManagerMessengerMessage;
