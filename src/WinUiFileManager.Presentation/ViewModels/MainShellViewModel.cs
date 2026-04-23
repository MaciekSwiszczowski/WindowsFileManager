using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
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
    private const double MinVisibleInspectorWidth = 260d;

    private readonly ISettingsRepository _settingsRepository;
    private readonly CopySelectionCommandHandler _copyHandler;
    private readonly MoveSelectionCommandHandler _moveHandler;
    private readonly DeleteSelectionCommandHandler _deleteHandler;
    private readonly CreateFolderCommandHandler _createFolderHandler;
    private readonly CopyFullPathCommandHandler _copyFullPathHandler;
    private readonly AddFavouriteCommandHandler _addFavouriteHandler;
    private readonly RemoveFavouriteCommandHandler _removeFavouriteHandler;
    private readonly OpenFavouriteCommandHandler _openFavouriteHandler;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly IDialogService _dialogService;
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly ILogger<MainShellViewModel> _logger;
    private readonly IDisposable _inspectorImmediateSubscription;
    private readonly IDisposable _inspectorDeferredSubscription;
    private string _inspectorSelectionSignature = string.Empty;
    private long _inspectorRefreshVersion;

    private AppSettings _currentSettings = new();

    [ObservableProperty]
    public partial FilePaneViewModel LeftPane { get; set; }

    [ObservableProperty]
    public partial FilePaneViewModel RightPane { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePaneLabel))]
    public partial FilePaneViewModel ActivePane { get; set; }

    [ObservableProperty]
    public partial FileInspectorViewModel Inspector { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumnWidth))]
    [NotifyPropertyChangedFor(nameof(InspectorMinWidth))]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    public partial double LeftPaneWidth { get; set; } = 600d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumnWidth))]
    public partial double InspectorWidth { get; set; } = 340d;

    public WindowPlacement MainWindowPlacement { get; set; } = WindowPlacement.Default;

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
        _copyFullPathHandler = copyFullPathHandler;
        _addFavouriteHandler = addFavouriteHandler;
        _removeFavouriteHandler = removeFavouriteHandler;
        _openFavouriteHandler = openFavouriteHandler;
        _setParallelExecutionHandler = setParallelExecutionHandler;
        _persistPaneStateHandler = persistPaneStateHandler;
        _dialogService = dialogService;
        _favouritesRepository = favouritesRepository;
        _logger = logger;
        Inspector = inspector;

        LeftPane = leftPane;
        RightPane = rightPane;
        leftPane.PaneId = PaneId.Left;
        rightPane.PaneId = PaneId.Right;
        ActivePane = leftPane;
        leftPane.IsActive = true;

        var inspectorSelections = CreateInspectorSelectionSignalObservable()
            .Select(forceRefresh => CreateCurrentInspectorSelection(forceRefresh))
            .Publish()
            .RefCount();

        _inspectorImmediateSubscription = inspectorSelections
            .ObserveOn(schedulers.MainThread)
            .Subscribe(
                Inspector.ApplySelection,
                ex => _logger.LogError(ex, "Inspector immediate update failed"));

        _inspectorDeferredSubscription = inspectorSelections
            .ObserveOn(schedulers.Background)
            .Throttle(InspectorDeferredLoadThrottle, schedulers.Background)
            .Select(selection => selection.CanLoadDeferred
                ? Observable.Create<FileInspectorDeferredBatchResult>(observer =>
                {
                    var cancellation = new CancellationDisposable();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await foreach (var batch in Inspector.LoadDeferredBatchesAsync(selection, cancellation.Token))
                            {
                                observer.OnNext(batch);
                            }
                        }
                        catch (OperationCanceledException) when (cancellation.Token.IsCancellationRequested)
                        {
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Inspector deferred batch streaming failed");
                        }
                        finally
                        {
                            observer.OnCompleted();
                        }
                    }, cancellation.Token);

                    return cancellation;
                })
                : Observable.Empty<FileInspectorDeferredBatchResult>())
            .Switch()
            .ObserveOn(schedulers.MainThread)
            .Subscribe(
                Inspector.ApplyDeferredBatch,
                ex => _logger.LogError(ex, "Inspector deferred update failed"));
    }

    public ObservableCollection<FavouriteFolder> Favourites { get; } = [];

    public FilePaneViewModel InactivePane => ActivePane == LeftPane ? RightPane : LeftPane;

    public string ActivePaneLabel => $"{ActivePane.PaneLabel} active";

    public double InspectorColumnWidth
    {
        get => IsInspectorVisible
            ? Math.Max(InspectorWidth, MinVisibleInspectorWidth)
            : 0d;
        set
        {
            if (value <= 0d)
            {
                return;
            }

            InspectorWidth = Math.Max(value, MinVisibleInspectorWidth);
        }
    }

    public double InspectorMinWidth => IsInspectorVisible ? MinVisibleInspectorWidth : 0d;

    public void UpdateInspectorWidthFromLayout(double width)
    {
        if (width <= 0d)
        {
            return;
        }

        InspectorWidth = Math.Max(width, MinVisibleInspectorWidth);
    }

    partial void OnActivePaneChanged(FilePaneViewModel value)
    {
        LeftPane.IsActive = value == LeftPane;
        RightPane.IsActive = value == RightPane;
        OnPropertyChanged(nameof(InactivePane));
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
            var hasDirectories = items.Any(static i => i.Kind == ItemKind.Directory);
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
            FocusActivePaneRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create folder operation failed");
        }
    }

    [RelayCommand]
    private Task RenameAsync()
    {
        var currentItem = ActivePane.CurrentItem;
        if (currentItem is null || currentItem.EntryKind == FileEntryKind.Parent)
        {
            return Task.CompletedTask;
        }

        ActivePane.BeginRenameCurrent();
        return Task.CompletedTask;
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
        }
    }

    [RelayCommand]
    private async Task OpenFavouriteAsync(FavouriteFolderId id)
    {
        try
        {
            var result = await _openFavouriteHandler.ExecuteAsync(id, CancellationToken.None);
            if (result is { Success: true, Path: not null })
            {
                await ActivePane.NavigateToCommand.ExecuteAsync(result.Path.Value.DisplayPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open favourite failed");
        }
    }

    [RelayCommand]
    private Task RefreshActivePaneAsync() => ActivePane.RefreshCommand.ExecuteAsync(null);

    public async Task InitializeAsync()
    {
        try
        {
            _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
            OnPropertyChanged(nameof(ParallelExecutionEnabled));
            IsInspectorVisible = _currentSettings.InspectorVisible;
            InspectorWidth = _currentSettings.InspectorWidth;
            LeftPaneWidth = _currentSettings.LeftPaneWidth;
            MainWindowPlacement = _currentSettings.MainWindowPlacement;

            LeftPane.ColumnLayout = _currentSettings.LeftPaneColumns;
            RightPane.ColumnLayout = _currentSettings.RightPaneColumns;
            LeftPane.ApplySortState(_currentSettings.LeftPaneSort);
            RightPane.ApplySortState(_currentSettings.RightPaneSort);

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
            var request = new PersistPaneStateRequest(
                LeftPanePath: LeftPane.CurrentNormalizedPath,
                RightPanePath: RightPane.CurrentNormalizedPath,
                ActivePane: ActivePane.PaneId,
                InspectorVisible: IsInspectorVisible,
                InspectorWidth: InspectorWidth,
                LeftPaneWidth: LeftPaneWidth,
                LeftPaneColumns: LeftPane.ColumnLayout,
                RightPaneColumns: RightPane.ColumnLayout,
                LeftPaneSort: LeftPane.SortState,
                RightPaneSort: RightPane.SortState,
                MainWindowPlacement: MainWindowPlacement);

            await _persistPaneStateHandler.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persisting pane state failed");
        }
    }

    public event EventHandler? FocusActivePaneRequested;

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

    private IObservable<bool> CreateInspectorSelectionSignalObservable()
    {
        var activePaneChanges = Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Where(static args => args.EventArgs.PropertyName == nameof(ActivePane))
            .Select(static _ => false);

        var refreshRequests = Observable
            .FromEventPattern<EventHandler, EventArgs>(
                handler => Inspector.RefreshRequested += handler,
                handler => Inspector.RefreshRequested -= handler)
            .Select(static _ => true);

        var paneSelectionChanges = CreatePaneSelectionObservable(LeftPane)
            .Merge(CreatePaneSelectionObservable(RightPane))
            .Select(static _ => false);

        return Observable.Merge(
                paneSelectionChanges,
                activePaneChanges,
                refreshRequests)
            .StartWith(false);
    }

    private IObservable<Unit> CreatePaneSelectionObservable(FilePaneViewModel pane)
    {
        var selectionChanges = Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => pane.PropertyChanged += handler,
                handler => pane.PropertyChanged -= handler)
            .Where(static args => args.EventArgs.PropertyName is
                nameof(FilePaneViewModel.CurrentItem)
                or nameof(FilePaneViewModel.SelectedCount))
            .Select(static _ => Unit.Default);

        var loadCompletionChanges = Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => pane.PropertyChanged += handler,
                handler => pane.PropertyChanged -= handler)
            .Where(static args =>
                args.EventArgs.PropertyName == nameof(FilePaneViewModel.IsLoading)
                && args.Sender is FilePaneViewModel { IsLoading: false })
            .Select(static _ => Unit.Default);

        return selectionChanges.Merge(loadCompletionChanges);
    }

    private FileInspectorSelection CreateCurrentInspectorSelection(bool forceRefresh)
    {
        var selectedEntries = ActivePane.GetSelectedEntries();
        var selectionSignature = CreateInspectorSelectionSignature(selectedEntries);
        if (forceRefresh || !string.Equals(selectionSignature, _inspectorSelectionSignature, StringComparison.Ordinal))
        {
            _inspectorSelectionSignature = selectionSignature;
            _ = Interlocked.Increment(ref _inspectorRefreshVersion);
        }

        return FileInspectorSelection.FromSelection(
            selectedEntries,
            ActivePane.IsLoading,
            _inspectorRefreshVersion);
    }

    private static string CreateInspectorSelectionSignature(IReadOnlyList<FileEntryViewModel> selectedEntries)
    {
        if (selectedEntries.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            '\u001f',
            selectedEntries.Select(static entry =>
                entry.EntryKind == FileEntryKind.Parent
                    ? ".."
                    : entry.Model is { } model
                        ? model.FullPath.DisplayPath
                        : entry.Name));
    }

    public void Dispose()
    {
        _inspectorImmediateSubscription.Dispose();
        _inspectorDeferredSubscription.Dispose();
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
        var progress = new Progress<OperationProgressEvent>(
            progressEvent =>
            {
                OperationProgress.ReportProgress(progressEvent);
                progressDialog.ReportProgress(progressEvent);
            });

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
            FocusActivePaneRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            await progressDialog.CloseAsync(CancellationToken.None);
            OperationProgress.Reset();
        }
    }

}
