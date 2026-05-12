using DynamicData;
using DynamicData.Binding;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.FileInspector;
using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed class FileInspectorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly FileInspectorFieldState _fieldState;
    private readonly FileInspectorDeferredBatchPlan _deferredBatchPlan;
    private readonly FileInspectorDeferredLoader _deferredLoader;
    private readonly FileInspectorThumbnailMaterializer _thumbnailMaterializer;
    private readonly IMessenger _messenger;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly SourceList<SpecFileEntryViewModel> _selectedItemsSource = new();
    private long _currentSelectionVersion;
    private string _currentFullPath = string.Empty;
    private long _tableSelectionRefreshVersion;
    private IReadOnlyList<SpecFileEntryViewModel> _lastTableSelection = [];
    private FileInspectorSelection? _currentTableSelection;
    private string _activePanelIdentity = string.Empty;
    private FileInspectorSelectionMode _selectionMode;
    private bool _inspectorPanelVisible = true;
    private bool _disposed;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        IMessenger messenger)
        : this(fileIdentityService, clipboardService, shellService, schedulers, logger, subscribeToMessages: true, messenger)
    {
    }

    internal FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        bool subscribeToMessages,
        IMessenger messenger)
    {
        _fileIdentityService = fileIdentityService;
        _logger = logger;
        _messenger = messenger;

        var inspectorModel = new FileInspectorModelBuilder(
            NtfsFileInspectorCategory.CanToggleField,
            ToggleNtfsFlagAsync).Build();
        _fieldState = new FileInspectorFieldState(inspectorModel);
        Fields = _fieldState.Fields;
        Categories = _fieldState.Categories;

        _deferredBatchPlan = new FileInspectorDeferredBatchPlan(
            fileIdentityService,
            logger,
            () => _disposed);

        _deferredLoader = new FileInspectorDeferredLoader(
            schedulers,
            logger,
            _deferredBatchPlan.LoadAsync,
            ApplyDeferredBatch,
            () => _disposed);

        _thumbnailMaterializer = new FileInspectorThumbnailMaterializer(
            _fieldState,
            logger,
            () => _disposed,
            () => HasCurrentSelection,
            () => _currentSelectionVersion,
            () => RefreshVisibleCategories());

        Commands = new FileInspectorCommandsViewModel(
            clipboardService,
            shellService,
            () => _currentFullPath,
            () => Categories,
            () => Fields,
            RefreshFromCurrentTableSelection);

        if (!subscribeToMessages)
        {
            return;
        }

        _subscriptions.Add(_selectedItemsSource
            .Connect()
            .Bind(SelectedItems)
            .Subscribe(_ => OnPropertyChanged(nameof(SelectedItems))));

        var observable = _messenger
            .CreateObservable<FileTableSelectionChangedMessage>()
            .Where(message => IsSelectionFromActivePanel(message.Identity))
            .ObserveOn(schedulers.Background);

        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count == 0)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    UpdateSelectedItems([]);
                    ShowNoSelection();
                }));

        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count > 1)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    UpdateSelectedItems(message.SelectedItems);
                    ShowMultiSelection(message.SelectedItems);
                }));

        // immediate subscription to show cheap changes and update selection
        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count == 1)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    UpdateSelectedItems(message.SelectedItems);
                    ApplyBasicTableSelection(message.SelectedItems);
                }));

        // throttled subscription to load deferred details
        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count == 1)
                .Throttle(SelectionThrottle, schedulers.Background)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message => LoadDeferredTableSelection(message.SelectedItems)));

        _messenger.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
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
        _selectedItemsSource.Dispose();
        _messenger.UnregisterAll(this);
        _deferredLoader.Dispose();
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (!message.IsFocused)
        {
            return;
        }

        _activePanelIdentity = message.Identity;
        if (!_inspectorPanelVisible)
        {
            return;
        }

        var selectedItems = RequestSelectedItems(message.Identity);
        UpdateSelectedItems(selectedItems);
        ApplyTableSelection(selectedItems);
    }

    private void OnToggleInspectorRequested(object recipient, ToggleInspectorRequestedMessage message)
    {
        if (_inspectorPanelVisible == message.IsVisible)
        {
            return;
        }

        _inspectorPanelVisible = message.IsVisible;
        if (!_inspectorPanelVisible)
        {
            _deferredLoader.Cancel();
            IsLoadingDetails = false;
            return;
        }

        RefreshFromCurrentTableSelection();
    }

    public FileInspectorCommandsViewModel Commands { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> SelectedItems { get; } = [];

    public FileInspectorSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (SetProperty(ref _selectionMode, value))
            {
                OnPropertyChanged(nameof(SelectionStatusText));
                OnPropertyChanged(nameof(IsNoSelectionMode));
                OnPropertyChanged(nameof(IsSingleSelectionMode));
                OnPropertyChanged(nameof(IsMultiSelectionMode));
            }
        }
    }

    public bool IsNoSelectionMode => SelectionMode == FileInspectorSelectionMode.NoSelection;

    public bool IsSingleSelectionMode => SelectionMode == FileInspectorSelectionMode.SingleSelection;

    public bool IsMultiSelectionMode => SelectionMode == FileInspectorSelectionMode.MultiSelection;

    public string SelectionStatusText => SelectionMode switch
    {
        FileInspectorSelectionMode.MultiSelection => $"{SelectedItems.Count} items selected",
        FileInspectorSelectionMode.NoSelection => "No files selected",
        _ => string.Empty,
    };

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; }

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; }

    public bool HasVisibleFields => _fieldState.HasVisibleFields;

    public bool IsLoadingDetails { get; set => SetProperty(ref field, value); }

    public string SearchText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RefreshVisibleCategories();
            }
        }
    } = string.Empty;

    public void ApplySelection(FileInspectorSelection selection)
    {
        if (_disposed)
        {
            return;
        }

        var hadSelection = !string.IsNullOrWhiteSpace(_currentFullPath);
        if (!selection.HasItem)
        {
            SelectionMode = FileInspectorSelectionMode.NoSelection;
            Clear();
            return;
        }

        SelectionMode = FileInspectorSelectionMode.SingleSelection;
        var isSameItem = hadSelection
            && string.Equals(_currentFullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase);
        var isSameVersion = selection.RefreshVersion == _currentSelectionVersion;

        if (isSameItem && isSameVersion)
        {
            IsLoadingDetails = selection.CanLoadDeferred;
            return;
        }

        ApplyBasicSelection(selection);
        _currentSelectionVersion = selection.RefreshVersion;
        IsLoadingDetails = selection.CanLoadDeferred;
    }

    private void Clear()
    {
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        _fieldState.ClearValues();
        RefreshVisibleCategories();
    }

    public void ApplyDeferredBatch(FileInspectorDeferredBatchResult batchResult)
    {
        if (_disposed || !HasCurrentSelection)
        {
            return;
        }

        if (batchResult.SelectionVersion != _currentSelectionVersion)
        {
            return;
        }

        foreach (var update in batchResult.Updates)
        {
            _fieldState.SetValue(update.Key, update.Value);
            _fieldState.SetLoading(update.Key, false);
        }

        if (batchResult.Category == FileInspectorCategory.Thumbnails)
        {
            _ = _thumbnailMaterializer.ApplyAsync(batchResult.SelectionVersion, batchResult.ThumbnailBytes);
        }

        RefreshVisibleCategories();

        if (batchResult.IsFinalBatch)
        {
            IsLoadingDetails = false;
        }
    }

    internal void ApplyBasicTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        _ = Interlocked.Increment(ref _tableSelectionRefreshVersion);

        var selection = FileInspectorSelection.FromSelection(
            selectedEntries,
            _tableSelectionRefreshVersion);

        _currentTableSelection = selection;
        ApplySelection(selection);
    }

    internal void LoadDeferredTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        if (!_inspectorPanelVisible)
        {
            return;
        }

        if (selectedEntries.Count != 1)
        {
            return;
        }

        var selection = _currentTableSelection?.RefreshVersion == _currentSelectionVersion
            ? _currentTableSelection
            : FileInspectorSelection.FromSelection(selectedEntries, _currentSelectionVersion);
        _deferredLoader.Start(selection);
    }

    internal void ShowNoSelection()
    {
        _lastTableSelection = [];
        _currentTableSelection = null;
        _deferredLoader.Cancel();
        SelectionMode = FileInspectorSelectionMode.NoSelection;
        var refreshVersion = Interlocked.Increment(ref _tableSelectionRefreshVersion);
        ApplySelection(FileInspectorSelection.NoSelection(refreshVersion));
    }

    internal void ShowMultiSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        _currentTableSelection = null;
        _deferredLoader.Cancel();
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        SelectionMode = FileInspectorSelectionMode.MultiSelection;
        _fieldState.ClearValues();
    }

    internal IAsyncEnumerable<FileInspectorDeferredBatchResult> LoadDeferredBatchesAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        return _deferredBatchPlan.LoadAsync(selection, cancellationToken);
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

    private void UpdateSelectedItems(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _selectedItemsSource.Edit(items =>
        {
            items.Clear();
            items.AddRange(selectedEntries);
        });
    }

    private void ApplyTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        ApplyBasicTableSelection(selectedEntries);
        LoadDeferredTableSelection(selectedEntries);
    }

    private void RefreshFromCurrentTableSelection()
    {
        if (string.IsNullOrWhiteSpace(_activePanelIdentity))
        {
            return;
        }

        var selectedItems = RequestSelectedItems(_activePanelIdentity);
        UpdateSelectedItems(selectedItems);

        if (selectedItems.Count == 0)
        {
            ShowNoSelection();
            return;
        }

        if (selectedItems.Count > 1)
        {
            ShowMultiSelection(selectedItems);
            return;
        }

        ApplyTableSelection(selectedItems);
    }

    private bool IsSelectionFromActivePanel(string identity)
    {
        return !string.IsNullOrWhiteSpace(_activePanelIdentity)
            && string.Equals(_activePanelIdentity, identity, StringComparison.Ordinal);
    }

    private void ApplyBasicSelection(FileInspectorSelection selection)
    {
        BasicFileInspectorCategory.ApplySelection(selection, _fieldState);
        _currentFullPath = selection.FullPath;

        NtfsFileInspectorCategory.ApplyAttributes(selection.AttributesFlags, _fieldState);
        _fieldState.BeginDeferredRefresh();

        RefreshVisibleCategories();
    }

    private async Task<bool> ToggleNtfsFlagAsync(string key, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath)
            || !NtfsFileInspectorCategory.TryGetToggleFlag(key, out var flag))
        {
            return false;
        }

        try
        {
            var updated = await _fileIdentityService.SetNtfsAttributeFlagAsync(
                _currentFullPath,
                flag,
                enabled,
                CancellationToken.None);

            if (updated)
            {
                ApplyTableSelection(_lastTableSelection);
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle NTFS flag {Flag} for {Path}", key, _currentFullPath);
            return false;
        }
    }

    private void RefreshVisibleCategories()
    {
        if (_disposed)
        {
            return;
        }

        _fieldState.RefreshVisibleCategories(
            _currentFullPath,
            SearchText);
        OnPropertyChanged(nameof(HasVisibleFields));
    }

    private bool HasCurrentSelection => !string.IsNullOrWhiteSpace(_currentFullPath);
}
