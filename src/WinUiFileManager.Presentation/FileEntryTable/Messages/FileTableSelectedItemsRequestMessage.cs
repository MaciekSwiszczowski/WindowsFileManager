using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Request-response message used to query the current real selected rows for one table.
/// </summary>
public sealed class FileTableSelectedItemsRequestMessage : AsyncRequestMessage<IReadOnlyList<SpecFileEntryViewModel>>, IFileManagerMessengerMessage
{
    public FileTableSelectedItemsRequestMessage(string identity) => Identity = identity;

    public string Identity { get; }
}
