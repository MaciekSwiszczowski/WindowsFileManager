using System.Reactive.Disposables;
using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;
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
    private readonly InspectorAttributeToggleViewModel _attributeToggle;
    private readonly IReadOnlyList<IInspectorDeferredFieldLoader> _deferredFieldLoaders;
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
        InspectorSearchViewModel search,
        InspectorAttributeToggleViewModel attributeToggle,
        IEnumerable<IInspectorDeferredFieldLoader> deferredFieldLoaders,
        FileEntryDisplayStringCache displayStringCache)
    {
        _messenger = messenger;
        _activePanelsService = activePanelsService;
        Categories = initialization.Categories;
        _fieldValueUpdater = new InspectorFieldValueUpdater(Categories, displayStringCache);
        _attributeToggle = attributeToggle;
        RefreshButton = refreshButton;
        PropertiesButton = propertiesButton;
        CopyToClipboardButton = copyToClipboardButton;
        Search = search;
        _deferredFieldLoaders = deferredFieldLoaders.ToList();

        CopyToClipboardButton.Initialize(() => Categories);
        Search.Initialize(Categories);
        _attributeToggle.Initialize(Categories);
        foreach (var loader in _deferredFieldLoaders)
        {
            if (loader is not IInspectorDeferredFieldLoaderInitializer initializer)
            {
                throw new InvalidOperationException($"{loader.GetType().Name} cannot be initialized.");
            }

            initializer.Initialize(_fieldValueUpdater);
            _subscriptions.Add(loader);
        }

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
        CancelDeferredFieldLoads();
        PropertiesButton.SetSelectedItem(null);
        _attributeToggle.SetSelectedItem(null);
        SetSelectedItemCount(selectedItems.Count);
        SelectionMode = selectedItems.Count == 0
            ? FileInspectorSelectionMode.NoSelection
            : FileInspectorSelectionMode.MultiSelection;
    }

    private void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        CancelDeferredFieldLoads();
        PropertiesButton.SetSelectedItem(selectedItem.Model);
        _attributeToggle.SetSelectedItem(selectedItem.Model);
        SetSelectedItemCount(1);
        _fieldValueUpdater.ShowImmediateSelection(selectedItem);
        Search.Refresh();
        SelectionMode = FileInspectorSelectionMode.SingleSelection;
    }

    private void LoadDeferredSelection(SpecFileEntryViewModel selectedItem)
    {
        foreach (var loader in _deferredFieldLoaders)
        {
            loader.Load(selectedItem);
        }
    }

    private void CancelDeferredFieldLoads()
    {
        foreach (var loader in _deferredFieldLoaders)
        {
            loader.Cancel();
        }
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
