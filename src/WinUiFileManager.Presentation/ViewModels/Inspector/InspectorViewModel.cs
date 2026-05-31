using System.Reactive.Disposables;
using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

/// <summary>
/// View model backing the inspector panel. Subscribes to the selection pipeline produced by
/// <see cref="InspectorInitializationViewModel"/> and drives what the panel shows: the multi/empty selection
/// summary, the immediate (synchronous) single-item fields, and the deferred (throttled, async) diagnostics
/// loaded by the <see cref="IInspectorDeferredFieldLoader"/>s. Also owns the inspector toolbar button view models.
/// </summary>
/// <remarks>
/// <para>
/// Subscriptions: all Rx subscriptions and the deferred-field loaders are added to <see cref="_subscriptions"/>
/// (a <see cref="CompositeDisposable"/>) and released in <see cref="Dispose"/> (AGENTS.md §5). The three selection
/// streams are gated by <c>Where(_ =&gt; _inspectorPanelVisible)</c> so a hidden inspector does no work.
/// </para>
/// <para>
/// Messaging: registers <see cref="ToggleInspectorRequestedMessage"/> on the strong-reference messenger and
/// unregisters in <see cref="Dispose"/>. <see cref="_inspectorPanelVisible"/> is <c>volatile</c> because it is
/// read on the Rx pipeline (potentially background) and written from the messenger callback.
/// </para>
/// <para>Threading: the selection observables are observed on the UI thread upstream, so the handlers below run UI-affine.</para>
/// </remarks>
public sealed partial class InspectorViewModel : ObservableObject, IDisposable
{
    /// <summary>Holds every Rx subscription and the deferred-field loaders; disposed once on teardown.</summary>
    private readonly CompositeDisposable _subscriptions = [];
    private readonly IMessenger _messenger;
    private readonly IActivePanelsService _activePanelsService;
    private readonly InspectorFieldValueUpdater _fieldValueUpdater;
    private readonly InspectorAttributeToggleViewModel _attributeToggle;
    private readonly IReadOnlyList<IInspectorDeferredFieldLoader> _deferredFieldLoaders;
    private int _selectedItemCount;
    private volatile bool _inspectorPanelVisible = true;
    private bool _disposed;

    /// <summary>Toolbar refresh button (re-requests the current selection's diagnostics).</summary>
    public InspectorRefreshButtonViewModel RefreshButton { get; }

    /// <summary>Toolbar button that opens the Windows shell properties dialog for the selected item.</summary>
    public InspectorPropertiesButtonViewModel PropertiesButton { get; }

    /// <summary>Toolbar button that copies the visible field values to the clipboard.</summary>
    public InspectorCopyToClipboardButtonViewModel CopyToClipboardButton { get; }

    /// <summary>Field search/filter view model.</summary>
    public InspectorSearchViewModel Search { get; }

    /// <summary>Current selection mode; drives which inspector layout is shown. Private setter (set from handlers).</summary>
    [ObservableProperty]
    public partial FileInspectorSelectionMode SelectionMode { get; private set; }

    /// <summary>Pluralized status text for the multi/empty-selection summary.</summary>
    public string MultiSelectionStatusText => _selectedItemCount == 1
        ? "1 item selected"
        : $"{_selectedItemCount} items selected";

    /// <summary>The category sections (with their fields); shared instance created by initialization.</summary>
    public List<InspectorCategoryViewModel> Categories { get; }

    /// <summary>
    /// Wires up the inspector: captures the shared categories, builds the field-value updater, initializes the
    /// toolbar buttons / search / attribute toggle, validates and registers each deferred field loader, and
    /// subscribes to the three selection streams. All subscriptions and loaders are tracked for disposal.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a registered <see cref="IInspectorDeferredFieldLoader"/> does not also implement
    /// <see cref="IInspectorDeferredFieldLoaderInitializer"/> (it cannot be initialized).
    /// </exception>
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

    /// <summary>
    /// Disposes all Rx subscriptions and deferred-field loaders and unregisters from the messenger. Idempotent
    /// via <see cref="_disposed"/>. Must be reachable from inspector/window teardown to avoid rooting this
    /// instance through the strong-reference messenger.
    /// </summary>
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

    /// <summary>
    /// Handles inspector show/hide. When the panel becomes visible again it re-requests the current selection so
    /// fields populate immediately (work was suppressed while hidden). No-op when visibility is unchanged.
    /// </summary>
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

    /// <summary>
    /// Handles a zero- or multi-item selection: cancels any in-flight deferred loads, clears the properties/attribute
    /// targets, updates the count, and switches to <see cref="FileInspectorSelectionMode.NoSelection"/> or
    /// <see cref="FileInspectorSelectionMode.MultiSelection"/>.
    /// </summary>
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

    /// <summary>
    /// Handles a single-item selection synchronously: cancels deferred loads, points the properties/attribute
    /// view models at the model, fills the immediately-available fields, refreshes search, and switches to
    /// <see cref="FileInspectorSelectionMode.SingleSelection"/>. The slower diagnostics arrive later via
    /// <see cref="LoadDeferredSelection"/>.
    /// </summary>
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

    /// <summary>
    /// Kicks off the throttled, asynchronous diagnostics loads for the settled single selection (one per loader).
    /// Fed by the throttled <c>DeferredSelectionObservable</c> so rapid selection changes don't spam diagnostics.
    /// </summary>
    private void LoadDeferredSelection(SpecFileEntryViewModel selectedItem)
    {
        foreach (var loader in _deferredFieldLoaders)
        {
            loader.Load(selectedItem);
        }
    }

    /// <summary>Cancels every loader's in-flight async load (used whenever the selection changes or is cleared).</summary>
    private void CancelDeferredFieldLoads()
    {
        foreach (var loader in _deferredFieldLoaders)
        {
            loader.Cancel();
        }
    }

    /// <summary>
    /// Asks the active pane to re-emit its selection (via <see cref="RefreshInspectorRequestMessage"/>) so the
    /// inspector repopulates after being shown again. No-op when there is no active pane.
    /// </summary>
    private void RefreshFromCurrentSelection()
    {
        var activePanelIdentity = _activePanelsService.ActivePanelIdentity;
        if (!string.IsNullOrWhiteSpace(activePanelIdentity))
        {
            _messenger.Send(new RefreshInspectorRequestMessage());
        }
    }

    /// <summary>Updates the selected-item count and raises <see cref="MultiSelectionStatusText"/> when it changes.</summary>
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
