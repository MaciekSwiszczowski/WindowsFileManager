using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;

using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed class InspectorInitializationViewModel
{
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IActivePanelsService _activePanelsService;
    private readonly ISchedulerProvider _schedulers;
    private readonly IMessenger _messenger;

    public InspectorInitializationViewModel(IActivePanelsService activePanelsService, ISchedulerProvider schedulers, IMessenger messenger)
    {
        _activePanelsService = activePanelsService;
        _schedulers = schedulers;
        _messenger = messenger;

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

    public IObservable<IReadOnlyList<SpecFileEntryViewModel>> NonSingleSelectionObservable { get; }

    public IObservable<SpecFileEntryViewModel> ImmediateSelectionObservable { get; }

    public IObservable<SpecFileEntryViewModel> DeferredSelectionObservable { get; }

    public List<InspectorCategoryViewModel> Categories { get; }

    private static List<InspectorCategoryViewModel> CreateCategories()
    {
        static InspectorCategoryViewModel Category(
            FileInspectorCategory category,
            params InspectorFieldViewModel[] fields)
        {
            var viewModel = new InspectorCategoryViewModel(category);

            foreach (var field in fields)
            {
                viewModel.Fields.Add(field);
            }

            viewModel.RefreshVisibility();
            return viewModel;
        }

        static InspectorFieldViewModel Field(
            FileInspectorCategory category,
            string key,
            string tooltip) =>
            new(category, key, tooltip);

        static InspectorThumbnailFieldViewModel ThumbnailField(
            string key,
            string tooltip) =>
            new(Thumbnails, key, tooltip);

        static InspectorToggleFieldViewModel ToggleField(
            string key,
            string tooltip) =>
            new(Ntfs, key, tooltip);

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

    private bool IsSelectionFromActivePanel(string identity) => _activePanelsService.ActivePanelIdentity == identity;
}
