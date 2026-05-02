using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Request-response message used to query the current real selected rows for one table.
/// </summary>
public sealed class FileTableSelectedItemsRequestMessage : RequestMessage<IReadOnlyList<SpecFileEntryViewModel>>
{
    public FileTableSelectedItemsRequestMessage(string identity) => Identity = identity;

    public string Identity { get; }
}
