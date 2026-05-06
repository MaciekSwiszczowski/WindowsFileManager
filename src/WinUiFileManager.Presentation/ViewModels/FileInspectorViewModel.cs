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
    private readonly CompositeDisposable _subscriptions = new();
    private readonly SourceList<SpecFileEntryViewModel> _selectedItemsSource = new();
    private long _currentSelectionVersion;
    private string _currentFullPath = string.Empty;
    private long _tableSelectionRefreshVersion;
    private IReadOnlyList<SpecFileEntryViewModel> _lastTableSelection = [];
    private FileInspectorSelection? _currentTableSelection;
    private string _activePanelIdentity = string.Empty;
    private string _lastSelectionIdentity = string.Empty;
    private bool _lastIsParentRowSelected;
    private SpecFileEntryViewModel? _lastActiveItem;
    private bool _preserveDeferredVisibilityUntilFinalBatch;
    private bool _isLoadingDetails;
    private string _searchText = string.Empty;
    private bool _disposed;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger)
        : this(
            fileIdentityService,
            clipboardService,
            shellService,
            schedulers,
            logger,
            subscribeToMessages: true)
    {
    }

    internal FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        bool subscribeToMessages)
    {
        _fileIdentityService = fileIdentityService;
        _logger = logger;

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
            () => _hasCurrentSelection,
            () => _currentSelectionVersion,
            () => RefreshVisibleCategories());

        Commands = new FileInspectorCommandsViewModel(
            clipboardService,
            shellService,
            () => _currentFullPath,
            () => Categories,
            () => Fields,
            CreateRefreshSelectionMessage);

        if (!subscribeToMessages)
        {
            return;
        }

        _subscriptions.Add(_selectedItemsSource
            .Connect()
            .Bind(SelectedItems)
            .Subscribe(_ => OnPropertyChanged(nameof(SelectedItems))));

        var observable = WeakReferenceMessenger.Default
            .CreateObservable<FileTableSelectionChangedMessage>()
            .Where(message => IsSelectionFromActivePanel(message.Identity))
            .ObserveOn(schedulers.Background);

        _subscriptions.Add(
            observable
                .Where(static message => message.SelectedItems.Count == 0)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    CaptureSelectionMessage(message);
                    UpdateSelectedItems([]);
                    ShowNoSelection();
                }));

        _subscriptions.Add(
            observable
                .Where(static message => message.SelectedItems.Count == 1)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
                    CaptureSelectionMessage(message);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscriptions.Dispose();
        _selectedItemsSource.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _deferredLoader.Dispose();
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            _activePanelIdentity = message.Identity;
            var selectedItems = RequestSelectedItems(message.Identity);
            CaptureSelectionMessage(
                new FileTableSelectionChangedMessage(
                    message.Identity,
                    selectedItems,
                    IsParentRowSelected: false,
                    ActiveItem: selectedItems.Count == 1 ? selectedItems[0] : null));
            UpdateSelectedItems(selectedItems);
            ApplyTableSelection(selectedItems);
        }
    }

    public FileInspectorCommandsViewModel Commands { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> SelectedItems { get; } = [];

    public ObservableCollection<FileInspectorFieldViewModel> Fields { get; }

    public ObservableCollection<FileInspectorCategoryViewModel> Categories { get; }

    public bool HasVisibleFields => _fieldState.HasVisibleFields;

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        set => SetProperty(ref _isLoadingDetails, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshVisibleCategories();
            }
        }
    }

    public void ApplySelection(FileInspectorSelection selection)
    {
        if (_disposed)
        {
            return;
        }

        var hadSelection = !string.IsNullOrWhiteSpace(_currentFullPath);
        if (!selection.HasItem)
        {
            Clear();
            return;
        }

        var isSameItem = hadSelection
            && string.Equals(_currentFullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase);
        var isSameVersion = selection.RefreshVersion == _currentSelectionVersion;

        if (isSameItem && isSameVersion)
        {
            IsLoadingDetails = selection.CanLoadDeferred;
            return;
        }

        var preserveDeferredVisibility = hadSelection;
        ApplyBasicSelection(selection, preserveDeferredVisibility);
        _currentSelectionVersion = selection.RefreshVersion;
        IsLoadingDetails = selection.CanLoadDeferred;
    }

    public void Clear()
    {
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        _preserveDeferredVisibilityUntilFinalBatch = false;
        _fieldState.ClearValues();
        RefreshVisibleCategories();
    }

    public void ApplyDeferredBatch(FileInspectorDeferredBatchResult batchResult)
    {
        if (_disposed || !_hasCurrentSelection)
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

        if (batchResult.IsFinalBatch)
        {
            _preserveDeferredVisibilityUntilFinalBatch = false;
            RefreshVisibleCategories();
        }
        else
        {
            RefreshVisibleCategories(_preserveDeferredVisibilityUntilFinalBatch);
        }

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
        var refreshVersion = Interlocked.Increment(ref _tableSelectionRefreshVersion);
        ApplySelection(FileInspectorSelection.NoSelection(refreshVersion));
    }

    internal IAsyncEnumerable<FileInspectorDeferredBatchResult> LoadDeferredBatchesAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        return _deferredBatchPlan.LoadAsync(selection, cancellationToken);
    }

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

    private void ApplyTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        ApplyBasicTableSelection(selectedEntries);
        LoadDeferredTableSelection(selectedEntries);
    }

    private void CaptureSelectionMessage(FileTableSelectionChangedMessage message)
    {
        _lastSelectionIdentity = message.Identity;
        _lastIsParentRowSelected = message.IsParentRowSelected;
        _lastActiveItem = message.ActiveItem;
    }

    private bool IsSelectionFromActivePanel(string identity)
    {
        return !string.IsNullOrWhiteSpace(_activePanelIdentity)
            && string.Equals(_activePanelIdentity, identity, StringComparison.Ordinal);
    }

    private FileTableSelectionChangedMessage? CreateRefreshSelectionMessage()
    {
        if (string.IsNullOrWhiteSpace(_lastSelectionIdentity))
        {
            return null;
        }

        return new FileTableSelectionChangedMessage(
            _lastSelectionIdentity,
            _lastTableSelection,
            _lastIsParentRowSelected,
            _lastActiveItem);
    }

    private void ApplyBasicSelection(FileInspectorSelection selection, bool preserveDeferredVisibility)
    {
        BasicFileInspectorCategory.ApplySelection(selection, _fieldState);
        _currentFullPath = selection.FullPath;

        if (preserveDeferredVisibility)
        {
            _fieldState.BeginDeferredRefresh();
        }
        else
        {
            NtfsFileInspectorCategory.ApplyAttributes(selection.AttributesFlags, _fieldState);
            _fieldState.ClearDeferredFields();
        }

        _preserveDeferredVisibilityUntilFinalBatch = preserveDeferredVisibility;
        RefreshVisibleCategories(preserveDeferredVisibility);
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

    private void RefreshVisibleCategories(bool preserveDeferredVisibility = false)
    {
        if (_disposed)
        {
            return;
        }

        _fieldState.RefreshVisibleCategories(
            _currentFullPath,
            SearchText,
            preserveDeferredVisibility);
        OnPropertyChanged(nameof(HasVisibleFields));
    }

    private bool _hasCurrentSelection => !string.IsNullOrWhiteSpace(_currentFullPath);
}
