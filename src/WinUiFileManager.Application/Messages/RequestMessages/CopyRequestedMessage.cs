using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to copy selected items from the source pane to the target pane's directory. Produced
/// by the source pane behavior from a <see cref="WinUiFileManager.Application.Messages.CopyKeyPressedMessage"/>;
/// handled by the file-operations layer.
/// </summary>
/// <param name="SourceIdentity">Identity of the pane the items came from.</param>
/// <param name="TargetIdentity">Identity of the destination pane.</param>
/// <param name="Items">The entries to copy.</param>
public sealed record CopyRequestedMessage(
    string SourceIdentity,
    string TargetIdentity,
    IReadOnlyList<FileSystemEntryModel> Items) : IFileManagerMessengerMessage;
