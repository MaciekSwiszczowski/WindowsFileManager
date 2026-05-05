using DynamicData;
using DynamicData.Binding;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.FileInspector;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FileInspectorViewModel : FileInspectorDetailsViewModelBase
{
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly CompositeDisposable _subscriptions = new();
    private readonly SourceList<SpecFileEntryViewModel> _selectedItemsSource = new();
    private bool _disposed;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger)
        : base(
            fileIdentityService,
            clipboardService,
            shellService,
            schedulers,
            logger)
    {
        _subscriptions.Add(_selectedItemsSource
            .Connect()
            .Bind(SelectedItems)
            .Subscribe(_ => OnPropertyChanged(nameof(SelectedItems))));
        _subscriptions.Add(_selectedItemsSource);

        var observable = WeakReferenceMessenger.Default
            .CreateObservable<FileTableSelectionChangedMessage>()
            .ObserveOn(schedulers.Background);

        _subscriptions.Add(
            observable
                .Where(static message => message.SelectedItems.Count == 0)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(_ =>
                {
                    UpdateSelectedItems([]);
                    ShowNoSelection();
                }));

        _subscriptions.Add(
            observable
                .Where(static message => message.SelectedItems.Count == 1)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    UpdateSelectedItems(message.SelectedItems);
                    ApplyBasicTableSelection(message.SelectedItems);
                }));

        _subscriptions.Add(
            observable
                .Where(static message => message.SelectedItems.Count == 1)
                .Throttle(SelectionThrottle, schedulers.Background)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message => LoadDeferredTableSelection(message.SelectedItems)));

        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriptions.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.Dispose();
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            var selectedItems = RequestSelectedItems(message.Identity);
            UpdateSelectedItems(selectedItems);
            ApplyTableSelection(selectedItems);
        }
    }

    public ObservableCollectionExtended<SpecFileEntryViewModel> SelectedItems { get; } = [];

    private static IReadOnlyList<SpecFileEntryViewModel> RequestSelectedItems(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return [];
        }

        var request = WeakReferenceMessenger.Default.Send(new FileTableSelectedItemsRequestMessage(identity));
        return request.HasReceivedResponse ? request.Response : [];
    }

    private void UpdateSelectedItems(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _selectedItemsSource.Edit(items =>
        {
            items.Clear();
            items.AddRange(selectedEntries);
        });
    }
}
