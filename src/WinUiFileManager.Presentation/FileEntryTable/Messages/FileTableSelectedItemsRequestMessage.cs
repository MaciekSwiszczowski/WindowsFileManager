using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

/// <summary>
/// Request-response message used to query the current real selected rows (excluding the parent row)
/// for one table. Answered by <see cref="Behaviors.FileEntryTableKeyboardSelectionBehavior"/>, which
/// marshals the snapshot onto the UI thread before replying.
/// </summary>
public sealed class FileTableSelectedItemsRequestMessage : AsyncRequestMessage<IReadOnlyList<FileListingRow>>, IIdentityMessage
{
    /// <param name="identity">The pane whose selection is being requested.</param>
    public FileTableSelectedItemsRequestMessage(Identity identity) => Identity = identity;

    /// <summary>The pane identity used to route this request to the matching table.</summary>
    public Identity Identity { get; }
}
