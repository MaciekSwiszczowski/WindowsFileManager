using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to copy the paths of selected items to the clipboard. Produced by the source pane
/// behavior from a <see cref="WinUiFileManager.Application.Messages.CopyPathKeyPressedMessage"/>; handled by the file-operations layer.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the items came from.</param>
/// <param name="Items">The selected entries whose paths should be copied.</param>
public sealed record CopyPathRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileSystemEntryModel> Items) : IFileManagerMessengerMessage;
