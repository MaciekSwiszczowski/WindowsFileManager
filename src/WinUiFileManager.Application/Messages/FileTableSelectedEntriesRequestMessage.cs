using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Request-response message used to query the current real selected entries for one table.
/// </summary>
public sealed class FileTableSelectedEntriesRequestMessage : RequestMessage<IReadOnlyList<FileSystemEntryModel>>, IFileManagerMessengerMessage
{
    public FileTableSelectedEntriesRequestMessage(string identity) => Identity = identity;

    public string Identity { get; }
}
