using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Loads one group of expensive, asynchronously-fetched inspector fields (links, locks, security, streams, cloud,
/// identity, thumbnail) for the selected item. Implementations are registered with <see cref="InspectorViewModel"/>,
/// which calls <see cref="Load"/> on the throttled deferred-selection stream and <see cref="Cancel"/> when the
/// selection changes.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/>: the inspector adds each loader to its <c>CompositeDisposable</c> so loads
/// are cancelled on teardown. Implementations must tolerate <see cref="Cancel"/>/<see cref="Load"/> superseding an
/// in-flight load.
/// </remarks>
public interface IInspectorDeferredFieldLoader : IDisposable
{
    /// <summary>Starts (or restarts) the asynchronous load for the given selection, superseding any in-flight load.</summary>
    public void Load(SpecFileEntryViewModel selectedItem);

    /// <summary>Cancels any in-flight load and clears the loading state on its fields.</summary>
    public void Cancel();
}
