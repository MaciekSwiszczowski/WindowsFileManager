using System.Reactive.Disposables;
using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed partial class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly IMessenger _messenger;
    private readonly IActivePanelsService _activePanelsService;
    private readonly InspectorFieldValueUpdater _fieldValueUpdater;
    private int _selectedItemCount;
    private volatile bool _inspectorPanelVisible = true;
    private bool _disposed;

    public InspectorRefreshButtonViewModel RefreshButton { get; }

    public InspectorPropertiesButtonViewModel PropertiesButton { get; }

    public InspectorCopyToClipboardButtonViewModel CopyToClipboardButton { get; }

    public InspectorSearchViewModel Search { get; }

    [ObservableProperty]
    public partial FileInspectorSelectionMode SelectionMode { get; private set; }

    public string MultiSelectionStatusText => _selectedItemCount == 1
        ? "1 item selected"
        : $"{_selectedItemCount} items selected";

    public List<InspectorCategoryViewModel> Categories { get; }

    public InspectorViewModel(
        InspectorInitializationViewModel initialization,
        IMessenger messenger,
        IActivePanelsService activePanelsService,
        InspectorRefreshButtonViewModel refreshButton,
        InspectorPropertiesButtonViewModel propertiesButton,
        InspectorCopyToClipboardButtonViewModel copyToClipboardButton,
        InspectorSearchViewModel search)
    {
        _messenger = messenger;
        _activePanelsService = activePanelsService;
        Categories = initialization.Categories;
        _fieldValueUpdater = new InspectorFieldValueUpdater(Categories);
        RefreshButton = refreshButton;
        PropertiesButton = propertiesButton;
        CopyToClipboardButton = copyToClipboardButton;
        Search = search;

        CopyToClipboardButton.Initialize(() => Categories);

        _subscriptions.Add(initialization
            .NonSingleSelectionObservable
            .Where(_ => _inspectorPanelVisible)
            .Subscribe(ShowNonSingleSelection));
        _subscriptions.Add(initialization
            .ImmediateSelectionObservable
            .Where(_ => _inspectorPanelVisible)
            .Subscribe(ShowImmediateSelection));
        _subscriptions.Add(initialization
            .DeferredSelectionObservable
            .Where(_ => _inspectorPanelVisible)
            .Subscribe(LoadDeferredSelection));

        _messenger.Register<ToggleInspectorRequestedMessage>(this, OnToggleInspectorRequested);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriptions.Dispose();
        _messenger.UnregisterAll(this);
    }

    private void OnToggleInspectorRequested(object recipient, ToggleInspectorRequestedMessage message)
    {
        if (_inspectorPanelVisible == message.IsVisible)
        {
            return;
        }

        _inspectorPanelVisible = message.IsVisible;
        if (_inspectorPanelVisible)
        {
            RefreshFromCurrentSelection();
        }
    }

    private void ShowNonSingleSelection(IReadOnlyList<SpecFileEntryViewModel> selectedItems)
    {
        PropertiesButton.SetSelectedItem(null);
        SetSelectedItemCount(selectedItems.Count);
        SelectionMode = selectedItems.Count == 0
            ? FileInspectorSelectionMode.NoSelection
            : FileInspectorSelectionMode.MultiSelection;
    }

    private void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        PropertiesButton.SetSelectedItem(selectedItem.Model);
        SetSelectedItemCount(1);
        _fieldValueUpdater.ShowImmediateSelection(selectedItem);
        SelectionMode = FileInspectorSelectionMode.SingleSelection;
    }

    private void LoadDeferredSelection(SpecFileEntryViewModel selectedItem)
    {
    }

    private void RefreshFromCurrentSelection()
    {
        var activePanelIdentity = _activePanelsService.ActivePanelIdentity;
        if (!string.IsNullOrWhiteSpace(activePanelIdentity))
        {
            _messenger.Send(new RefreshInspectorRequestMessage());
        }
    }

    private void SetSelectedItemCount(int value)
    {
        if (_selectedItemCount == value)
        {
            return;
        }

        _selectedItemCount = value;
        OnPropertyChanged(nameof(MultiSelectionStatusText));
    }
}
