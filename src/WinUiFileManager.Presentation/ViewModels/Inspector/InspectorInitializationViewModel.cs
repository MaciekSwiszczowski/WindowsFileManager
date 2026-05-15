using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed class InspectorInitializationViewModel
{
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IActivePanelsService _activePanelsService;
    private readonly ISchedulerProvider _schedulers;
    private readonly IMessenger _messenger;

    public InspectorInitializationViewModel(
        IActivePanelsService activePanelsService,
        ISchedulerProvider schedulers,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(activePanelsService);
        ArgumentNullException.ThrowIfNull(schedulers);
        ArgumentNullException.ThrowIfNull(messenger);

        _activePanelsService = activePanelsService;
        _schedulers = schedulers;
        _messenger = messenger;

        var selectionChanges = CreateSelectionChanges();

        NonSingleSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count != 1)
            .ObserveOn(_schedulers.MainThread)
            .Select(static message => message.SelectedItems);

        ImmediateSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count == 1)
            .ObserveOn(_schedulers.MainThread)
            .Select(static message => message.SelectedItems);

        DeferredSelectionObservable = selectionChanges
            .Where(static message => message.SelectedItems.Count == 1)
            .Throttle(SelectionThrottle, _schedulers.Background)
            .ObserveOn(_schedulers.MainThread)
            .Select(static message => message.SelectedItems);
    }

    public IObservable<IReadOnlyList<SpecFileEntryViewModel>> NonSingleSelectionObservable { get; }

    public IObservable<IReadOnlyList<SpecFileEntryViewModel>> ImmediateSelectionObservable { get; }

    public IObservable<IReadOnlyList<SpecFileEntryViewModel>> DeferredSelectionObservable { get; }

    private IObservable<FileTableSelectionChangedMessage> CreateSelectionChanges()
    {
        var tableSelectionObservable = _messenger
            .CreateObservable<FileTableSelectionChangedMessage>()
            .Where(message => IsSelectionFromActivePanel(message.Identity));

        var focusSelectionObservable = _messenger
            .CreateObservable<FileTableFocusedMessage>()
            .Where(static message => message.IsFocused)
            .ObserveOn(_schedulers.MainThread)
            .Select(message => CreateSelectionChangedMessage(message.Identity));

        return tableSelectionObservable
            .Merge(focusSelectionObservable)
            .ObserveOn(_schedulers.Background);
    }

    private FileTableSelectionChangedMessage CreateSelectionChangedMessage(string identity)
    {
        var selectedItems = RequestSelectedItems(identity);
        return new FileTableSelectionChangedMessage(
            identity,
            selectedItems,
            IsParentRowSelected: false,
            ActiveItem: selectedItems.Count == 1 ? selectedItems[0] : null);
    }

    private IReadOnlyList<SpecFileEntryViewModel> RequestSelectedItems(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return [];
        }

        var request = _messenger.Send(new FileTableSelectedItemsRequestMessage(identity));
        return request.HasReceivedResponse ? request.Response : [];
    }

    private bool IsSelectionFromActivePanel(string identity) => _activePanelsService.ActivePanelIdentity == identity;
}
