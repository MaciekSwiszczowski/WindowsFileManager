using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;

using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

/// <summary>
/// Builds the inspector's category/field tree once and constructs the Rx selection pipeline that
/// <see cref="InspectorViewModel"/> subscribes to. Splits raw selection-change/refresh messages into three
/// derived observables: non-single (empty/multi), immediate single, and a throttled deferred single stream.
/// </summary>
/// <remarks>
/// <para>
/// The source observables are cold (created from the messenger via <c>CreateObservable</c>): each subscriber owns
/// its own subscription, and this type holds no subscriptions itself, so it owns nothing disposable. Disposal of
/// the derived subscriptions is the consumer's responsibility (<see cref="InspectorViewModel"/> tracks them).
/// </para>
/// <para>
/// Threading: selection messages are observed on the background scheduler for filtering/async fan-out, then the
/// derived streams switch to <see cref="ISchedulerProvider.MainThread"/> so handlers run UI-affine. The deferred
/// stream additionally throttles by <see cref="SelectionThrottle"/> so transient selections don't trigger
/// expensive diagnostics.
/// </para>
/// </remarks>
public sealed class InspectorInitializationViewModel
{
    /// <summary>Quiet period a single selection must persist before deferred diagnostics are requested.</summary>
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IActivePanelsService _activePanelsService;
    private readonly ISchedulerProvider _schedulers;
    private readonly IMessenger _messenger;
    private readonly Func<FileInspectorCategory, InspectorCategoryViewModel> _categoryFactory;
    private readonly Func<InspectorFieldCreationRequest, InspectorBasicFieldViewModel> _fieldFactory;
    private readonly Func<InspectorFieldCreationRequest, InspectorThumbnailFieldViewModel> _thumbnailFieldFactory;
    private readonly Func<InspectorFieldCreationRequest, InspectorToggleFieldViewModel> _toggleFieldFactory;

    /// <summary>
    /// Creates the category tree and the three derived selection observables. The field/category factories are
    /// injected (DI) so each created view model is container-resolved.
    /// </summary>
    public InspectorInitializationViewModel(
        IActivePanelsService activePanelsService,
        ISchedulerProvider schedulers,
        IMessenger messenger,
        Func<FileInspectorCategory, InspectorCategoryViewModel> categoryFactory,
        Func<InspectorFieldCreationRequest, InspectorBasicFieldViewModel> fieldFactory,
        Func<InspectorFieldCreationRequest, InspectorThumbnailFieldViewModel> thumbnailFieldFactory,
        Func<InspectorFieldCreationRequest, InspectorToggleFieldViewModel> toggleFieldFactory)
    {
        _activePanelsService = activePanelsService;
        _schedulers = schedulers;
        _messenger = messenger;
        _categoryFactory = categoryFactory;
        _fieldFactory = fieldFactory;
        _thumbnailFieldFactory = thumbnailFieldFactory;
        _toggleFieldFactory = toggleFieldFactory;

        Categories = CreateCategories();

        var selectionChanges = CreateSelectionChanges();

        NonSingleSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count != 1)
            .ObserveOn(_schedulers.MainThread)
            .Select(static message => message.SelectedItems);

        ImmediateSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count == 1)
            .Select(static message => message.SelectedItems.First())
            .ObserveOn(_schedulers.MainThread);

        DeferredSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count == 1)
            .Select(static message => message.SelectedItems.First())
            .Throttle(SelectionThrottle, _schedulers.Background)
            .ObserveOn(_schedulers.MainThread);
    }

    /// <summary>Emits when the selection is empty or has more than one item (UI thread).</summary>
    public IObservable<IReadOnlyList<SpecFileEntryViewModel>> NonSingleSelectionObservable { get; }

    /// <summary>Emits the single selected item immediately, for synchronously-available fields (UI thread).</summary>
    public IObservable<SpecFileEntryViewModel> ImmediateSelectionObservable { get; }

    /// <summary>Emits the single selected item after <see cref="SelectionThrottle"/>, for expensive diagnostics (UI thread).</summary>
    public IObservable<SpecFileEntryViewModel> DeferredSelectionObservable { get; }

    /// <summary>The fully-built category sections with their fields; shared with <see cref="InspectorViewModel"/>.</summary>
    public List<InspectorCategoryViewModel> Categories { get; }

    /// <summary>
    /// Builds the inspector's category/field tree. Local helpers (<c>Category</c>/<c>Field</c>/<c>ToggleField</c>/
    /// <c>ThumbnailField</c>) keep the declarative layout readable; the field <c>Key</c> strings are the contract
    /// used by <see cref="InspectorFieldValueUpdater"/> and the deferred loaders to address fields by name.
    /// </summary>
    private List<InspectorCategoryViewModel> CreateCategories()
    {
        InspectorCategoryViewModel Category(
            FileInspectorCategory category,
            params InspectorFieldViewModelBase[] fields)
        {
            var viewModel = _categoryFactory(category);

            foreach (var field in fields)
            {
                viewModel.Fields.Add(field);
            }

            viewModel.RefreshVisibility();
            return viewModel;
        }

        InspectorBasicFieldViewModel Field(FileInspectorCategory category, string key, string tooltip) =>
            _fieldFactory(new InspectorFieldCreationRequest(category, key, tooltip, string.Empty));

        InspectorThumbnailFieldViewModel ThumbnailField(string key, string tooltip) =>
            _thumbnailFieldFactory(new InspectorFieldCreationRequest(Thumbnails, key, tooltip, string.Empty));

        InspectorToggleFieldViewModel ToggleField(string key, string tooltip) =>
            _toggleFieldFactory(new InspectorFieldCreationRequest(Ntfs, key, tooltip, string.Empty));

        return
        [
            Category(
                Basic,
                Field(Basic, "Name", "File or folder name"),
                Field(Basic, "Full Path", "Full selected item path"),
                Field(Basic, "Type", "Item type"),
                Field(Basic, "Extension", "File extension"),
                Field(Basic, "Size", "Size in a human-readable format"),
                Field(Basic, "Attributes", "File system attributes")),

            Category(
                Ntfs,
                Field(Ntfs, "Created", "NTFS creation time in UTC."),
                Field(Ntfs, "Accessed", "NTFS last access time in UTC."),
                Field(Ntfs, "Modified", "NTFS last write time in UTC."),
                Field(Ntfs, "MFT Changed", "NTFS metadata change time in UTC."),
                ToggleField("Read Only", "Whether the item is marked read-only."),
                ToggleField("Hidden", "Whether the item is hidden."),
                ToggleField("Archive", "Whether the archive attribute is set."),
                Field(Ntfs, "Encrypted", "Whether the item is encrypted with EFS."),
                Field(Ntfs, "Compressed", "Whether the item is compressed by NTFS."),
                Field(Ntfs, "Reparse Point", "Whether the item is a reparse point.")),

            Category(
                Ids,
                Field(Ids, "File ID", "128-bit NTFS identifier for the selected file system entry."),
                Field(Ids, "Volume Serial", "Volume serial number of the drive that contains the item."),
                Field(
                    Ids,
                    "File Index (64-bit)",
                    "Older 64-bit file index from the legacy Windows API. Diagnostic/compatibility value only."),
                Field(Ids, "Hard Link Count", "How many hard links point to the same file record, when available."),
                Field(Ids, "Final Path", "The resolved final path reported by Windows.")),

            Category(
                Locks,
                Field(
                    Locks,
                    "Is locked",
                    "Whether the selected item appears to be locked based on the other lock diagnostics in this category."),
                Field(Locks, "In Use", "Whether Windows currently reports the item as in use. Best-effort diagnostic."),
                Field(Locks, "Locked By", "Applications or services that Windows reports as using this item."),
                Field(
                    Locks,
                    "Lock PIDs",
                    "Process IDs of applications using this item. Useful in Task Manager or Process Explorer."),
                Field(Locks, "Lock Services", "Service names associated with the lock, when available.")),

            Category(
                Links,
                Field(Links, "Link Target", "Target path of a symbolic link, junction, or shell shortcut."),
                Field(Links, "Link Status", "What kind of link Windows reports for the item."),
                Field(Links, "Reparse Tag", "Reparse point classification reported by Windows."),
                Field(Links, "Reparse Data", "Additional reparse data, when Windows can provide it."),
                Field(Links, "Object ID", "NTFS object identifier, when available.")),

            Category(
                Streams,
                Field(Streams, "Alternate Stream Count", "How many alternate data streams the item has."),
                Field(Streams, "Alternate Streams", "Names and sizes of alternate data streams.")),

            Category(
                Security,
                Field(Security, "Owner", "Owner of the file or folder."),
                Field(Security, "Group", "Primary group of the file or folder."),
                Field(Security, "DACL Summary", "Summary of access rules from the discretionary access control list."),
                Field(Security, "SACL Summary", "Summary of audit rules from the system access control list."),
                Field(Security, "Inherited", "Whether the permissions are inherited."),
                Field(Security, "Protected", "Whether inherited permissions are blocked.")),

            Category(
                Thumbnails,
                ThumbnailField("Thumbnail", "Thumbnail preview reported by Windows, when available."),
                Field(Thumbnails, "Has Thumbnail", "Whether Windows could provide a thumbnail for the selected item."),
                Field(
                    Thumbnails,
                    "Association",
                    "Shell association or file type hint used for the thumbnail, when available.")),

            Category(
                Cloud,
                Field(
                    Cloud,
                    "Status",
                    "Combined cloud-file state summary such as hydrated, dehydrated, pinned, synced, or uploading."),
                Field(Cloud, "Provider", "Cloud provider display name."),
                Field(Cloud, "Sync Root", "Owning sync-root path or display name."),
                Field(Cloud, "Root ID", "Sync-root registration identifier."),
                Field(Cloud, "Provider ID", "Provider identifier from the sync-root registration."),
                Field(Cloud, "Available", "Whether the selected item is currently available locally."),
                Field(
                    Cloud,
                    "Transfer",
                    "Current transfer state such as upload, download, or paused, when Windows exposes it."),
                Field(Cloud, "Custom", "Provider-defined custom cloud status text, when available.")),
        ];
    }

    /// <summary>
    /// Merges two selection sources into one stream: live table selection changes scoped to the active pane, and
    /// focus-driven refresh requests that synthesize a selection by querying the active pane on demand. Both run on
    /// the background scheduler; downstream derived observables marshal to the UI thread.
    /// </summary>
    private IObservable<FileTableSelectionChangedMessage> CreateSelectionChanges()
    {
        var tableSelectionObservable = _messenger
            .CreateObservable<FileTableSelectionChangedMessage>()
            .ObserveOn(_schedulers.Background)
            .Where(message => IsSelectionFromActivePanel(message.Identity));

        var focusSelectionObservable = _messenger
            .CreateObservable<RefreshInspectorRequestMessage>()
            .ObserveOn(_schedulers.Background)
            .Select(_ => _activePanelsService.ActivePanelIdentity)
            .Where(static identity => !string.IsNullOrWhiteSpace(identity))
            .SelectMany(identity => Observable.FromAsync(() => CreateSelectionChangedMessageAsync(identity)));

        return tableSelectionObservable
            .Merge(focusSelectionObservable);
    }

    /// <summary>
    /// Builds a selection-changed message for a focus refresh by requesting the active pane's selected items.
    /// Library-style async (uses <c>ConfigureAwait(false)</c>).
    /// </summary>
    private async Task<FileTableSelectionChangedMessage> CreateSelectionChangedMessageAsync(string identity)
    {
        var selectedItems = await RequestSelectedItemsAsync(identity).ConfigureAwait(false);

        // The inspector only consumes SelectedItems from this focus-refresh adapter.
        // Parent-row visual state and active-row state remain owned by the table behaviors, we can safely do any defaults here.
        return new FileTableSelectionChangedMessage(
            identity,
            selectedItems,
            IsParentRowSelected: false,
            ActiveItem: selectedItems.Count == 1 ? selectedItems[0] : null);
    }

    /// <summary>
    /// Sends a <see cref="FileTableSelectedItemsRequestMessage"/> and awaits the responder's reply, returning an
    /// empty list when no recipient answers (or identity is blank). Library-style async.
    /// </summary>
    private async Task<IReadOnlyList<SpecFileEntryViewModel>> RequestSelectedItemsAsync(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return [];
        }

        var request = _messenger.Send(new FileTableSelectedItemsRequestMessage(identity));
        return request.HasReceivedResponse
            ? await request.Response.ConfigureAwait(false)
            : [];
    }

    /// <summary>True when a selection-change message originates from the currently active pane.</summary>
    private bool IsSelectionFromActivePanel(string identity) => _activePanelsService.ActivePanelIdentity == identity;
}
