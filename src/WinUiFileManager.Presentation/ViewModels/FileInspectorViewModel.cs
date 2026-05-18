using System.Reactive.Linq;
using System.Reactive.Disposables;
using WinUiFileManager.Application.Messages.RequestMessages.FileOperations;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.FileInspector;
using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed class FileInspectorViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan SelectionThrottle = TimeSpan.FromMilliseconds(300);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly IActivePanelsService _activePanelsService;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly FileInspectorFieldState _fieldState;
    private readonly FileInspectorSelectionFieldUpdater _selectionFieldUpdater;
    private readonly FileInspectorDeferredBatchPlan _deferredBatchPlan;
    private readonly FileInspectorDeferredLoader _deferredLoader;
    private readonly FileInspectorThumbnailMaterializer _thumbnailMaterializer;
    private readonly IMessenger _messenger;
    private readonly CompositeDisposable _subscriptions = new();
    private long _currentSelectionVersion;
    private string _currentFullPath = string.Empty;
    private long _tableSelectionRefreshVersion;
    private IReadOnlyList<SpecFileEntryViewModel> _lastTableSelection = [];
    private FileInspectorSelection? _currentTableSelection;
    private int _selectedItemCount;
    private bool _inspectorPanelVisible = true;
    private bool _disposed;

    public FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        IActivePanelsService activePanelsService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        IMessenger messenger)
        : this(fileIdentityService, clipboardService, shellService, activePanelsService, schedulers, logger, subscribeToMessages: true, messenger)
    {
    }

    internal FileInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        IActivePanelsService activePanelsService,
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        bool subscribeToMessages,
        IMessenger messenger)
    {
        _fileIdentityService = fileIdentityService;
        _activePanelsService = activePanelsService;
        _logger = logger;
        _messenger = messenger;

        var inspectorModel = new FileInspectorModelBuilder(
            NtfsFileInspectorCategory.CanToggleField,
            ToggleNtfsFlagAsync).Build();
        _fieldState = new FileInspectorFieldState(inspectorModel);
        _selectionFieldUpdater = new FileInspectorSelectionFieldUpdater(_fieldState);
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
            schedulers,
            _fieldState,
            logger,
            () => _disposed,
            () => HasCurrentSelection,
            () => _currentSelectionVersion,
            RefreshVisibleCategories);

        Commands = new FileInspectorCommandsViewModel(
            clipboardService,
            shellService,
            () => _currentFullPath,
            () => Categories,
            () => Fields,
            () => _ = RefreshFromCurrentTableSelectionAsync());

        if (!subscribeToMessages)
        {
            return;
        }

        var tableSelectionObservable = _messenger
            .CreateObservable<FileTableSelectionChangedMessage>()
            .Where(message => IsSelectionFromActivePanel(message.Identity));

        var focusSelectionObservable = _messenger
            .CreateObservable<FileTableFocusedMessage>()
            .Where(static message => message.IsFocused)
            .ObserveOn(schedulers.MainThread)
            .SelectMany(message => Observable.FromAsync(() => CreateSelectionChangedMessageAsync(message.Identity)));

        var observable = tableSelectionObservable
            .Merge(focusSelectionObservable)
            .ObserveOn(schedulers.Background);

        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count == 0)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(_ =>
                {
                    ShowNoSelection();
                }));

        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count > 1)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message =>
                {
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
                    ShowImmediateTableSelection(message.SelectedItems);
                }));

        // throttled subscription to load deferred details
        _subscriptions.Add(
            observable
                .Where(_ => _inspectorPanelVisible)
                .Where(static message => message.SelectedItems.Count == 1)
                .Throttle(SelectionThrottle, schedulers.Background)
                .ObserveOn(schedulers.MainThread)
                .Subscribe(message => LoadDeferredTableSelection(message.SelectedItems)));

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
        _deferredLoader.Dispose();
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

        _ = RefreshFromCurrentTableSelectionAsync();
    }

    public FileInspectorCommandsViewModel Commands { get; }

    private FileInspectorSelectionMode SelectionMode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
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
        FileInspectorSelectionMode.MultiSelection => $"{_selectedItemCount} items selected",
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

    public void ShowSelection(FileInspectorSelection selection)
    {
        if (_disposed)
        {
            return;
        }

        if (!selection.HasItem)
        {
            ShowEmptySelection(selection);
            return;
        }

        if (IsCurrentSelection(selection))
        {
            IsLoadingDetails = selection.CanLoadDeferred;
            return;
        }

        ShowSingleSelection(selection);
    }

    private void ShowEmptySelection(FileInspectorSelection selection)
    {
        SelectionMode = FileInspectorSelectionMode.NoSelection;
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        _currentSelectionVersion = selection.RefreshVersion;
        _selectionFieldUpdater.HideAllFields();
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

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            RefreshVisibleCategories();
        }

        if (batchResult.IsFinalBatch)
        {
            IsLoadingDetails = false;
        }
    }

    private void ShowImmediateTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        SetSelectedItemCount(selectedEntries.Count);
        _ = Interlocked.Increment(ref _tableSelectionRefreshVersion);

        var selection = FileInspectorSelection.FromSelection(
            selectedEntries,
            _tableSelectionRefreshVersion);

        _currentTableSelection = selection;
        ShowSelection(selection);
    }

    private void LoadDeferredTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
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

    private void ShowNoSelection()
    {
        _lastTableSelection = [];
        _currentTableSelection = null;
        _deferredLoader.Cancel();
        SetSelectedItemCount(0);
        var refreshVersion = Interlocked.Increment(ref _tableSelectionRefreshVersion);
        ShowSelection(FileInspectorSelection.NoSelection(refreshVersion));
    }

    private void ShowMultiSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        _lastTableSelection = selectedEntries;
        _currentTableSelection = null;
        _deferredLoader.Cancel();
        SetSelectedItemCount(selectedEntries.Count);
        IsLoadingDetails = false;
        _currentFullPath = string.Empty;
        SelectionMode = FileInspectorSelectionMode.MultiSelection;
        _selectionFieldUpdater.HideAllFields();
    }

    internal IAsyncEnumerable<FileInspectorDeferredBatchResult> LoadDeferredBatchesAsync(
        FileInspectorSelection selection,
        CancellationToken cancellationToken)
    {
        return _deferredBatchPlan.LoadAsync(selection, cancellationToken);
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

    private async Task<FileTableSelectionChangedMessage> CreateSelectionChangedMessageAsync(string identity)
    {
        var selectedItems = await RequestSelectedItemsAsync(identity).ConfigureAwait(false);
        return new FileTableSelectionChangedMessage(
            identity,
            selectedItems,
            IsParentRowSelected: false,
            ActiveItem: selectedItems.Count == 1 ? selectedItems[0] : null);
    }

    private void ShowAndLoadTableSelection(IReadOnlyList<SpecFileEntryViewModel> selectedEntries)
    {
        ShowImmediateTableSelection(selectedEntries);
        LoadDeferredTableSelection(selectedEntries);
    }

    private async Task RefreshFromCurrentTableSelectionAsync()
    {
        var activePanelIdentity = _activePanelsService.ActivePanelIdentity;
        if (string.IsNullOrWhiteSpace(activePanelIdentity))
        {
            return;
        }

        var selectedItems = await RequestSelectedItemsAsync(activePanelIdentity).ConfigureAwait(false);

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

        ShowAndLoadTableSelection(selectedItems);
    }

    private bool IsSelectionFromActivePanel(string identity)
    {
        var activePanelIdentity = _activePanelsService.ActivePanelIdentity;
        return !string.IsNullOrWhiteSpace(activePanelIdentity)
            && string.Equals(activePanelIdentity, identity, StringComparison.Ordinal);
    }

    private bool IsCurrentSelection(FileInspectorSelection selection)
    {
        return HasCurrentSelection
            && selection.RefreshVersion == _currentSelectionVersion
            && string.Equals(_currentFullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowSingleSelection(FileInspectorSelection selection)
    {
        SelectionMode = FileInspectorSelectionMode.SingleSelection;
        _currentFullPath = selection.FullPath;
        _currentSelectionVersion = selection.RefreshVersion;
        _selectionFieldUpdater.ShowImmediateSingleSelectionFields(selection);
        IsLoadingDetails = selection.CanLoadDeferred;

        RefreshVisibleCategories();
    }

    private void SetSelectedItemCount(int value)
    {
        if (_selectedItemCount == value)
        {
            return;
        }

        _selectedItemCount = value;
        OnPropertyChanged(nameof(SelectionStatusText));
    }

    private async Task<bool> ToggleNtfsFlagAsync(string key, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath)
            || !NtfsFileInspectorCategory.TryGetToggleFlag(key, out var flag))
        {
            return false;
        }

        _messenger.Send(new SetFileAttributeFlagRequestedMessage(
            NormalizedPath.FromUserInput(_currentFullPath),
            flag,
            enabled));
        return false;
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
