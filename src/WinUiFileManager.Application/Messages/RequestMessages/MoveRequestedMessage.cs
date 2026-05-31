using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to move selected items from the source pane to the target pane's directory. Produced
/// by the source pane behavior from a <see cref="WinUiFileManager.Application.Messages.MoveKeyPressedMessage"/>;
/// handled by the file-operations layer.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the items came from.</param>
/// <param name="TargetIdentity">Identity of the destination pane.</param>
/// <param name="Items">The entries to move.</param>
public sealed record MoveRequestedMessage(
    string SourceIdentity,
    string TargetIdentity,
    IReadOnlyList<FileSystemEntryModel> Items) : IFileManagerMessengerMessage;
