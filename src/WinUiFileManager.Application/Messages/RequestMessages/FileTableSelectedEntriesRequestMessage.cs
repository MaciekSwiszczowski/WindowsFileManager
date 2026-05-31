using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Request-response message used to synchronously query a specific pane's currently selected entries.
/// Sent by pane behaviors while resolving a command (e.g. copy/move/delete) to learn the selection;
/// the target table replies via the toolkit <see cref="RequestMessage{T}"/> mechanism. Identity-scoped.
/// </summary>
public sealed class FileTableSelectedEntriesRequestMessage : RequestMessage<IReadOnlyList<FileSystemEntryModel>>, IIdentityMessage
{
    /// <param name="identity">The pane whose selection is being queried.</param>
    public FileTableSelectedEntriesRequestMessage(Identity identity) => Identity = identity;

    /// <inheritdoc/>
    public Identity Identity { get; }
}
