using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Loads one group of expensive, asynchronously-fetched inspector fields (links, locks, security, streams, cloud,
/// identity, thumbnail) for the selected item. Implementations are registered with <see cref="InspectorViewModel"/>,
/// which calls <see cref="Prepare"/> on immediate single-selection changes, <see cref="Load"/> on the throttled
/// deferred-selection stream, and <see cref="Cancel"/> when the selection is cleared.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/>: the inspector adds each loader to its <c>CompositeDisposable</c> so loads
/// are cancelled on teardown. Implementations must tolerate <see cref="Prepare"/>/<see cref="Cancel"/>/
/// <see cref="Load"/> superseding an in-flight load.
/// </remarks>
public interface IInspectorDeferredFieldLoader : IDisposable
{
    /// <summary>
    /// Cancels any in-flight load and marks this loader's fields as waiting for refreshed diagnostics.
    /// The diagnostics request itself may still be throttled by the inspector selection pipeline.
    /// </summary>
    public void Prepare(FileListingRow selectedItem);

    /// <summary>Marks this loader as waiting for the next category response, superseding any in-flight load.</summary>
    public void Load(FileListingRow selectedItem);

    /// <summary>Cancels any in-flight load and clears the loading state on its fields.</summary>
    public void Cancel();
}
