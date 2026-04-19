using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan InspectorDeferredLoadThrottle = TimeSpan.FromMilliseconds(200);

    private readonly ISettingsRepository _settingsRepository;
    private readonly CopySelectionCommandHandler _copyHandler;
    private readonly MoveSelectionCommandHandler _moveHandler;
    private readonly DeleteSelectionCommandHandler _deleteHandler;
    private readonly CreateFolderCommandHandler _createFolderHandler;
    private readonly RenameEntryCommandHandler _renameHandler;
    private readonly CopyFullPathCommandHandler _copyFullPathHandler;
    private readonly AddFavouriteCommandHandler _addFavouriteHandler;
    private readonly RemoveFavouriteCommandHandler _removeFavouriteHandler;
    private readonly OpenFavouriteCommandHandler _openFavouriteHandler;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly IDialogService _dialogService;
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<MainShellViewModel> _logger;
    private readonly IDisposable _inspectorImmediateSubscription;
    private readonly IDisposable _inspectorDeferredSubscription;

    private AppSettings _currentSettings = new();

    [ObservableProperty]
    public partial FilePaneViewModel LeftPane { get; set; }

    [ObservableProperty]
    public partial FilePaneViewModel RightPane { get; set; }

    [ObservableProperty]
    public partial FilePaneViewModel ActivePane { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FileInspectorViewModel Inspector { get; set; }

    [ObservableProperty]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    public partial double InspectorWidth { get; set; } = 340d;

    public OperationProgressViewModel OperationProgress { get; } = new();

    public bool ParallelExecutionEnabled
    {
        get => _currentSettings.ParallelExecutionEnabled;
        set
        {
            if (_currentSettings.ParallelExecutionEnabled != value)
            {
                OnParallelExecutionEnabledChanged(value);
            }
        }
    }

    private async void OnParallelExecutionEnabledChanged(bool value)
    {
        try
        {
            await _setParallelExecutionHandler.ExecuteAsync(value, 4, CancellationToken.None);
            _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
            OnPropertyChanged(nameof(ParallelExecutionEnabled));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update parallel execution setting");
        }
    }

    public MainShellViewModel(
        ISettingsRepository settingsRepository,
        CopySelectionCommandHandler copyHandler,
        MoveSelectionCommandHandler moveHandler,
        DeleteSelectionCommandHandler deleteHandler,
        CreateFolderCommandHandler createFolderHandler,
        RenameEntryCommandHandler renameHandler,
        CopyFullPathCommandHandler copyFullPathHandler,
        AddFavouriteCommandHandler addFavouriteHandler,
        RemoveFavouriteCommandHandler removeFavouriteHandler,
        OpenFavouriteCommandHandler openFavouriteHandler,
        SetParallelExecutionCommandHandler setParallelExecutionHandler,
        PersistPaneStateCommandHandler persistPaneStateHandler,
        IDialogService dialogService,
        IFavouritesRepository favouritesRepository,
        ISchedulerProvider schedulers,
        ILogger<MainShellViewModel> logger,
        FileInspectorViewModel inspector,
        FilePaneViewModel leftPane,
        FilePaneViewModel rightPane)
    {
        _settingsRepository = settingsRepository;
        _copyHandler = copyHandler;
        _moveHandler = moveHandler;
        _deleteHandler = deleteHandler;
        _createFolderHandler = createFolderHandler;
        _renameHandler = renameHandler;
        _copyFullPathHandler = copyFullPathHandler;
        _addFavouriteHandler = addFavouriteHandler;
        _removeFavouriteHandler = removeFavouriteHandler;
        _openFavouriteHandler = openFavouriteHandler;
        _setParallelExecutionHandler = setParallelExecutionHandler;
        _persistPaneStateHandler = persistPaneStateHandler;
        _dialogService = dialogService;
        _favouritesRepository = favouritesRepository;
        _schedulers = schedulers;
        _logger = logger;
        Inspector = inspector;

        LeftPane = leftPane;
        RightPane = rightPane;
        ActivePane = leftPane;
        leftPane.IsActive = true;

        leftPane.PropertyChanged += OnPanePropertyChanged;
        rightPane.PropertyChanged += OnPanePropertyChanged;

        var inspectorSelectionSignals = CreateInspectorSelectionSignalObservable()
            .Publish()
            .RefCount();

        _inspectorImmediateSubscription = inspectorSelectionSignals
            .Select(_ => CreateCurrentInspectorSelection())
            .DistinctUntilChanged()
            .ObserveOn(_schedulers.MainThread)
            .Subscribe(
                Inspector.ApplySelection,
                ex => _logger.LogError(ex, "Inspector immediate update failed"));

        _inspectorDeferredSubscription = inspectorSelectionSignals
            .ObserveOn(_schedulers.Background)
            .Throttle(InspectorDeferredLoadThrottle, _schedulers.Background)
            .Select(_ => CreateCurrentInspectorSelection())
            .DistinctUntilChanged()
            .Select(selection => selection.CanLoadDeferred
                ? Observable.FromAsync(ct => Inspector.LoadDeferredBatchesAsync(selection, ct))
                    .SelectMany(static results => results.ToObservable())
                : Observable.Empty<FileInspectorDeferredBatchResult>())
            .Switch()
            .ObserveOn(_schedulers.MainThread)
            .Subscribe(
                Inspector.ApplyDeferredBatch,
                ex => _logger.LogError(ex, "Inspector deferred update failed"));
    }

    public ObservableCollection<FavouriteFolder> Favourites { get; } = [];

    public FilePaneViewModel InactivePane => ActivePane == LeftPane ? RightPane : LeftPane;

    partial void OnActivePaneChanged(FilePaneViewModel value)
    {
        LeftPane.IsActive = value == LeftPane;
        RightPane.IsActive = value == RightPane;
        OnPropertyChanged(nameof(InactivePane));
        UpdateStatusText();
    }

    [RelayCommand]
    private void SwitchActivePane()
    {
        ActivePane = ActivePane == LeftPane ? RightPane : LeftPane;
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (OperationProgress.IsRunning)
        {
            return;
        }

        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
        {
            return;
        }

        var destination = InactivePane.CurrentNormalizedPath;
        if (destination is null)
        {
            return;
        }

        try
        {
            var parallelOptions = new ParallelExecutionOptions(
                _currentSettings.ParallelExecutionEnabled,
                _currentSettings.MaxDegreeOfParallelism);

            await RunTrackedOperationAsync(
                OperationType.Copy,
                (progress, cancellationToken) => _copyHandler.ExecuteAsync(
                    items,
                    destination.Value,
                    CollisionPolicy.Ask,
                    parallelOptions,
                    progress,
                    cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy operation failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task MoveAsync()
    {
        if (OperationProgress.IsRunning)
        {
            return;
        }

        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
        {
            return;
        }

        var destination = InactivePane.CurrentNormalizedPath;
        if (destination is null)
        {
            return;
        }

        try
        {
            var parallelOptions = new ParallelExecutionOptions(
                _currentSettings.ParallelExecutionEnabled,
                _currentSettings.MaxDegreeOfParallelism);

            await RunTrackedOperationAsync(
                OperationType.Move,
                (progress, cancellationToken) => _moveHandler.ExecuteAsync(
                    items,
                    destination.Value,
                    CollisionPolicy.Ask,
                    parallelOptions,
                    progress,
                    cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Move operation failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (OperationProgress.IsRunning)
        {
            return;
        }

        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            var hasDirectories = items.Any(i => i.Kind == ItemKind.Directory);
            var confirmed = await _dialogService.ShowDeleteConfirmationAsync(
                items.Count, hasDirectories, CancellationToken.None);

            if (!confirmed)
            {
                return;
            }

            await RunTrackedOperationAsync(
                OperationType.Delete,
                (progress, cancellationToken) => _deleteHandler.ExecuteAsync(
                    items,
                    progress,
                    cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete operation failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var currentPath = ActivePane.CurrentNormalizedPath;
        if (currentPath is null)
        {
            return;
        }

        try
        {
            var folderName = await _dialogService.ShowCreateFolderDialogAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            var summary = await _createFolderHandler.ExecuteAsync(
                currentPath.Value,
                folderName,
                new Progress<OperationProgressEvent>(),
                CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);
            await ActivePane.RefreshCommand.ExecuteAsync(null);
            FocusActivePaneRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create folder operation failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        var currentItem = ActivePane.CurrentItem;
        if (currentItem is null || currentItem.IsParentEntry)
        {
            return;
        }

        try
        {
            var newName = await _dialogService.ShowRenameDialogAsync(
                currentItem.Name, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(newName) || newName == currentItem.Name)
            {
                return;
            }

            var summary = await _renameHandler.ExecuteAsync(
                currentItem.Model, newName, CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);
            await ActivePane.RefreshCommand.ExecuteAsync(null);
            FocusActivePaneRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename operation failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyFullPathAsync()
    {
        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
        {
            return;
        }

        try
        {
            await _copyFullPathHandler.ExecuteAsync(items, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy full path failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddFavouriteAsync()
    {
        var currentPath = ActivePane.CurrentNormalizedPath;
        if (currentPath is null)
        {
            return;
        }

        try
        {
            var displayName = Path.GetFileName(currentPath.Value.Value.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = currentPath.Value.DisplayPath;
            }

            var result = await _addFavouriteHandler.ExecuteAsync(
                displayName, currentPath.Value, CancellationToken.None);

            if (result.IsValid)
            {
                await LoadFavouritesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add favourite failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveFavouriteAsync(FavouriteFolderId id)
    {
        try
        {
            await _removeFavouriteHandler.ExecuteAsync(id, CancellationToken.None);
            await LoadFavouritesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remove favourite failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenFavouriteAsync(FavouriteFolderId id)
    {
        try
        {
            var result = await _openFavouriteHandler.ExecuteAsync(id, CancellationToken.None);
            if (result.Success && result.Path is not null)
            {
                await ActivePane.NavigateToCommand.ExecuteAsync(result.Path.Value.DisplayPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open favourite failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshActivePaneAsync()
    {
        await ActivePane.RefreshCommand.ExecuteAsync(null);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
            OnPropertyChanged(nameof(ParallelExecutionEnabled));
            IsInspectorVisible = _currentSettings.InspectorVisible;
            InspectorWidth = _currentSettings.InspectorWidth;

            await Task.WhenAll(
                LoadFavouritesAsync(),
                LeftPane.LoadDrivesAsync(),
                RightPane.LoadDrivesAsync());

            var defaultPath = LeftPane.AvailableDrives.FirstOrDefault()?.RootPath.DisplayPath
                ?? RightPane.AvailableDrives.FirstOrDefault()?.RootPath.DisplayPath
                ?? @"C:\";

            var leftPath = _currentSettings.LastLeftPanePath?.DisplayPath ?? defaultPath;
            var rightPath = _currentSettings.LastRightPanePath?.DisplayPath ?? defaultPath;

            await Task.WhenAll(
                NavigateWithFallbackAsync(LeftPane, leftPath, defaultPath),
                NavigateWithFallbackAsync(RightPane, rightPath, defaultPath));

            ActivePane = _currentSettings.LastActivePane == PaneId.Right ? RightPane : LeftPane;
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialization failed");
        }
    }

    private static async Task NavigateWithFallbackAsync(
        FilePaneViewModel pane,
        string preferredPath,
        string fallbackPath)
    {
        await pane.NavigateToCommand.ExecuteAsync(preferredPath);

        if (pane.CurrentNormalizedPath is null
            && !string.Equals(preferredPath, fallbackPath, StringComparison.OrdinalIgnoreCase))
        {
            await pane.NavigateToCommand.ExecuteAsync(fallbackPath);
        }
    }

    public async Task PersistStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _persistPaneStateHandler.ExecuteAsync(
                LeftPane.CurrentNormalizedPath,
                RightPane.CurrentNormalizedPath,
                ActivePane.PaneId,
                IsInspectorVisible,
                InspectorWidth,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persisting pane state failed");
        }
    }

    public event Action? FocusActivePaneRequested;

    public void UpdateStatusText()
    {
        var paneName = ActivePane.PaneId == PaneId.Left ? "Left" : "Right";
        StatusText = $"{paneName} | {ActivePane.ItemCount} items | {ActivePane.SelectedCount} selected | {ActivePane.CurrentPath}";
    }

    private async Task LoadFavouritesAsync()
    {
        var items = await _favouritesRepository.GetAllAsync(CancellationToken.None);
        Favourites.Clear();
        foreach (var item in items)
        {
            Favourites.Add(item);
        }
    }

    [RelayCommand]
    private void ToggleInspector()
    {
        IsInspectorVisible = !IsInspectorVisible;
    }

    public void SetInspectorWidth(double width)
    {
        InspectorWidth = Math.Clamp(width, 260d, 640d);
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not FilePaneViewModel pane)
        {
            return;
        }

        if (pane != ActivePane)
        {
            return;
        }

        if (e.PropertyName is nameof(FilePaneViewModel.CurrentItem)
            or nameof(FilePaneViewModel.SelectedCount)
            or nameof(FilePaneViewModel.IsLoading))
        {
            UpdateStatusText();
        }
    }

    private IObservable<Unit> CreateInspectorSelectionSignalObservable()
    {
        var activePaneChanges = Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Where(static args => args.EventArgs.PropertyName == nameof(ActivePane))
            .Select(static _ => Unit.Default);

        return Observable.Merge(
                CreatePaneSelectionObservable(LeftPane),
                CreatePaneSelectionObservable(RightPane),
                activePaneChanges)
            .StartWith(Unit.Default);
    }

    private IObservable<Unit> CreatePaneSelectionObservable(FilePaneViewModel pane)
    {
        return Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => pane.PropertyChanged += handler,
                handler => pane.PropertyChanged -= handler)
            .Where(static args => args.EventArgs.PropertyName is
                nameof(FilePaneViewModel.CurrentItem)
                or nameof(FilePaneViewModel.SelectedCount)
                or nameof(FilePaneViewModel.IsLoading))
            .Select(static _ => Unit.Default);
    }

    private FileInspectorSelection CreateCurrentInspectorSelection()
    {
        return FileInspectorSelection.FromSelection(
            ActivePane.GetSelectedEntries(),
            ActivePane.IsLoading);
    }

    public void Dispose()
    {
        LeftPane.PropertyChanged -= OnPanePropertyChanged;
        RightPane.PropertyChanged -= OnPanePropertyChanged;
        _inspectorImmediateSubscription.Dispose();
        _inspectorDeferredSubscription.Dispose();
        Inspector.Dispose();
    }

    private async Task RunTrackedOperationAsync(
        OperationType operationType,
        Func<IProgress<OperationProgressEvent>, CancellationToken, Task<OperationSummary>> executeAsync)
    {
        OperationProgress.Start(operationType);
        var progressDialog = await _dialogService.ShowOperationProgressAsync(
            operationType,
            () => OperationProgress.CancelCommand.Execute(null),
            CancellationToken.None);
        using var progress = new ThrottledSynchronizationContextProgress<OperationProgressEvent>(
            progressEvent =>
            {
                OperationProgress.ReportProgress(progressEvent);
                progressDialog.ReportProgress(progressEvent);
            },
            static progressEvent => progressEvent.CompletedItems == 0
                || progressEvent.TotalItems == 0
                || progressEvent.CompletedItems >= progressEvent.TotalItems);

        try
        {
            var summary = await executeAsync(progress, OperationProgress.CancellationToken);
            OperationProgress.Finish();
            await progressDialog.CloseAsync(CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);

            // Pane content is kept in sync by the active-folder directory change stream
            // (WindowsDirectoryChangeStream + the pane's Rx pipeline). No explicit refresh
            // is needed here: the self-inflicted watcher events coalesce into a few
            // buffered batches on a background scheduler while we were showing the result
            // dialog, and each batch commits a single SourceCache edit on the UI thread.
            FocusActivePaneRequested?.Invoke();
        }
        finally
        {
            await progressDialog.CloseAsync(CancellationToken.None);
            OperationProgress.Reset();
        }
    }

    private sealed class ThrottledSynchronizationContextProgress<T>(
        Action<T> handler,
        Func<T, bool>? shouldFlushImmediately = null) : IProgress<T>, IDisposable
    {
        private static readonly TimeSpan MinimumUpdateInterval = TimeSpan.FromMilliseconds(50);
        private readonly Lock _gate = new();
        private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
        private T? _pendingValue;
        private bool _hasPendingValue;
        private bool _flushScheduled;
        private long _lastPublishedTimestamp;

        public void Report(T value)
        {
            if (shouldFlushImmediately?.Invoke(value) == true || ShouldPublishNow())
            {
                Publish(value);
                return;
            }

            lock (_gate)
            {
                _pendingValue = value;
                _hasPendingValue = true;

                if (_flushScheduled)
                {
                    return;
                }

                _flushScheduled = true;
            }

            _ = FlushLaterAsync();
        }

        public void Dispose()
        {
            T? pendingValue = default;
            var hasPendingValue = false;

            lock (_gate)
            {
                if (_hasPendingValue)
                {
                    pendingValue = _pendingValue;
                    hasPendingValue = true;
                    _hasPendingValue = false;
                    _pendingValue = default;
                }
            }

            if (hasPendingValue)
            {
                Publish(pendingValue!);
            }
        }

        private bool ShouldPublishNow()
        {
            var lastPublishedTimestamp = Interlocked.Read(ref _lastPublishedTimestamp);
            if (lastPublishedTimestamp == 0)
            {
                return true;
            }

            return Stopwatch.GetElapsedTime(lastPublishedTimestamp) >= MinimumUpdateInterval;
        }

        private async Task FlushLaterAsync()
        {
            await Task.Delay(MinimumUpdateInterval).ConfigureAwait(false);

            T? pendingValue = default;
            var hasPendingValue = false;

            lock (_gate)
            {
                _flushScheduled = false;
                if (_hasPendingValue)
                {
                    pendingValue = _pendingValue;
                    hasPendingValue = true;
                    _hasPendingValue = false;
                    _pendingValue = default;
                }
            }

            if (hasPendingValue)
            {
                Publish(pendingValue!);
            }
        }

        private void Publish(T value)
        {
            Interlocked.Exchange(ref _lastPublishedTimestamp, Stopwatch.GetTimestamp());

            if (_synchronizationContext is null)
            {
                handler(value);
                return;
            }

            _synchronizationContext.Post(
                static state =>
                {
                    var (callback, reportedValue) = ((Action<T>, T))state!;
                    callback(reportedValue);
                },
                (handler, value));
        }
    }
}
