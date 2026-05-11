using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Request-response message used to query the current real selected entries for one table.
/// </summary>
public sealed class FileTableSelectedEntriesRequestMessage : RequestMessage<IReadOnlyList<FileSystemEntryModel>>, IFileManagerMessengerMessage
{
    public FileTableSelectedEntriesRequestMessage(string identity) => Identity = identity;

    public string Identity { get; }
}
