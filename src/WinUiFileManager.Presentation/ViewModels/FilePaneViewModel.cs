using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FilePaneViewModel : ObservableObject, IDisposable
{
    private const int InitialBatchSize = 150;
    private const int SubsequentBatchSize = 750;

    private readonly OpenEntryCommandHandler _openEntryHandler;
    private readonly IFileSystemService _fileSystemService;
    private readonly INtfsVolumePolicyService _volumePolicyService;
    private readonly IPathNormalizationService _pathNormalizationService;
    private readonly ILogger<FilePaneViewModel> _logger;
    private readonly SynchronizationContext? _uiSynchronizationContext;

    private readonly SourceCache<FileEntryViewModel, string> _sourceCache = new(static x => x.UniqueKey);
    private readonly BehaviorSubject<IComparer<FileEntryViewModel>> _sortComparer;
    private readonly ReadOnlyObservableCollection<FileEntryViewModel> _sortedItems;
    private readonly IDisposable _subscription;

    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _watchRefreshDebounceCancellation;
    private IDisposable? _directoryWatchSubscription;
    private NormalizedPath? _currentNormalizedPath;

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial FileEntryViewModel? CurrentItem { get; set; }

    [ObservableProperty]
    public partial string? IncrementalSearchText { get; private set; }

    [ObservableProperty]
    public partial SortColumn SortBy { get; set; } = SortColumn.Name;

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    public ObservableCollection<VolumeInfo> AvailableDrives { get; } = [];

    [ObservableProperty]
    public partial VolumeInfo? SelectedDrive { get; set; }

    public FilePaneViewModel(
        OpenEntryCommandHandler openEntryHandler,
        IFileSystemService fileSystemService,
        INtfsVolumePolicyService volumePolicyService,
        IPathNormalizationService pathNormalizationService,
        ILogger<FilePaneViewModel> logger)
    {
        _openEntryHandler = openEntryHandler;
        _fileSystemService = fileSystemService;
        _volumePolicyService = volumePolicyService;
        _pathNormalizationService = pathNormalizationService;
        _logger = logger;
        _uiSynchronizationContext = SynchronizationContext.Current;

        _sortComparer = new BehaviorSubject<IComparer<FileEntryViewModel>>(
            new FileEntryComparer(SortColumn.Name, true));

        _subscription = _sourceCache.Connect()
            .SortAndBind(out _sortedItems, _sortComparer.AsObservable())
            .Subscribe();
    }

    public PaneId PaneId { get; set; }

    public ReadOnlyObservableCollection<FileEntryViewModel> Items => _sortedItems;

    public int ItemCount => _sortedItems.Count;

    public int SelectedCount => _sortedItems.Count(static i => i.IsSelected);

    public NormalizedPath? CurrentNormalizedPath => _currentNormalizedPath;

    public bool IsInteractive => !IsLoading;

    public void SetSort(SortColumn column)
    {
        if (SortBy == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortBy = column;
            SortAscending = true;
        }

        _sortComparer.OnNext(new FileEntryComparer(SortBy, SortAscending));
    }

    [RelayCommand]
    private async Task NavigateToAsync(string path)
    {
        var normalizedPath = await ResolveDirectoryPathAsync(path, CancellationToken.None);
        if (normalizedPath is null)
        {
            return;
        }

        await LoadDirectoryAsync(normalizedPath.Value, null, CancellationToken.None);
    }

    [RelayCommand]
    private async Task NavigateIntoAsync()
    {
        if (IsLoading || CurrentItem is null)
        {
            return;
        }

        if (CurrentItem.IsParentEntry)
        {
            await NavigateUpAsync();
            return;
        }

        var targetPath = CurrentItem.Model.FullPath;

        if (!CurrentItem.IsDirectory)
        {
            try
            {
                await _openEntryHandler.ExecuteAsync(targetPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open {Path}", targetPath);
                ErrorMessage = ex.Message;
            }

            return;
        }

        await LoadDirectoryAsync(targetPath, null, CancellationToken.None);
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (IsLoading || _currentNormalizedPath is null)
        {
            return;
        }

        var oldPath = _currentNormalizedPath.Value;
        var parent = Path.GetDirectoryName(oldPath.Value);
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        var parentPath = NormalizedPath.FromUserInput(parent);
        var folderName = Path.GetFileName(oldPath.DisplayPath.TrimEnd(Path.DirectorySeparatorChar));
        await LoadDirectoryAsync(parentPath, folderName, CancellationToken.None);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading || _currentNormalizedPath is null)
        {
            return;
        }

        await RefreshCurrentDirectoryAsync(CancellationToken.None);
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (IsLoading)
        {
            return;
        }

        foreach (var item in _sortedItems.Where(static i => !i.IsParentEntry))
        {
            item.IsSelected = true;
        }

        NotifySelectionChanged();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (IsLoading)
        {
            return;
        }

        foreach (var item in _sortedItems)
        {
            item.IsSelected = false;
        }

        CurrentItem = null;
        NotifySelectionChanged();
    }

    public void ToggleSelection(FileEntryViewModel item)
    {
        if (IsLoading || item.IsParentEntry)
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        NotifySelectionChanged();
    }

    public void HandleIncrementalSearch(char c)
    {
        if (IsLoading)
        {
            return;
        }

        IncrementalSearchText = (IncrementalSearchText ?? string.Empty) + c;

        var match = _sortedItems.FirstOrDefault(i =>
            !i.IsParentEntry
            && i.Name.StartsWith(IncrementalSearchText, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            CurrentItem = match;
        }
    }

    public void ClearIncrementalSearch()
    {
        IncrementalSearchText = null;
    }

    public void BackspaceIncrementalSearch()
    {
        if (IsLoading || string.IsNullOrEmpty(IncrementalSearchText))
        {
            return;
        }

        IncrementalSearchText = IncrementalSearchText[..^1];
        if (string.IsNullOrEmpty(IncrementalSearchText))
        {
            ClearIncrementalSearch();
            return;
        }

        var match = _sortedItems.FirstOrDefault(i =>
            !i.IsParentEntry
            && i.Name.StartsWith(IncrementalSearchText, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            CurrentItem = match;
        }
    }

    public async Task LoadDrivesAsync()
    {
        var drives = await _volumePolicyService.GetNtfsVolumesAsync(CancellationToken.None);
        AvailableDrives.Clear();
        foreach (var drive in drives)
        {
            AvailableDrives.Add(drive);
        }
    }

    partial void OnSelectedDriveChanged(VolumeInfo? value)
    {
        if (value is not null && !IsLoading)
        {
            _ = NavigateToCommand.ExecuteAsync(value.RootPath.DisplayPath);
        }
    }

    public IReadOnlyList<FileSystemEntryModel> GetSelectedEntryModels()
    {
        if (IsLoading)
        {
            return [];
        }

        var selected = _sortedItems
            .Where(static i => i is { IsSelected: true, IsParentEntry: false })
            .Select(static i => i.Model)
            .ToList();

        if (selected.Count > 0)
        {
            return selected;
        }

        if (CurrentItem is { IsParentEntry: false })
        {
            return [CurrentItem.Model];
        }

        return [];
    }

    public IReadOnlyList<FileEntryViewModel> GetSelectedEntries()
    {
        if (IsLoading)
        {
            return [];
        }

        var selected = _sortedItems
            .Where(static i => i is { IsSelected: true, IsParentEntry: false })
            .ToList();

        if (selected.Count > 0)
        {
            return selected;
        }

        if (CurrentItem is { } current)
        {
            return [current];
        }

        return [];
    }

    public void RemoveItems(IEnumerable<string> fullPaths)
    {
        _sourceCache.Remove(fullPaths);
        NotifySelectionChanged();
        OnPropertyChanged(nameof(ItemCount));
    }

    public void AddOrUpdateItems(IEnumerable<FileEntryViewModel> items)
    {
        _sourceCache.AddOrUpdate(items);
        NotifySelectionChanged();
        OnPropertyChanged(nameof(ItemCount));
    }

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        CancelWatcherRefresh();
        DisposeDirectoryWatcher();
        _subscription.Dispose();
        _sortComparer.Dispose();
        _sourceCache.Dispose();
    }

    private async Task<NormalizedPath?> ResolveDirectoryPathAsync(string rawPath, CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        var pathValidation = _pathNormalizationService.Validate(rawPath);
        if (!pathValidation.IsValid)
        {
            ErrorMessage = pathValidation.ErrorMessage ?? "Navigation failed.";
            return null;
        }

        var normalizedPath = _pathNormalizationService.Normalize(rawPath);
        var ntfsValidation = _volumePolicyService.ValidateNtfsPath(normalizedPath.Value);
        if (!ntfsValidation.IsValid)
        {
            ErrorMessage = ntfsValidation.ErrorMessage ?? "Navigation failed.";
            return null;
        }

        var exists = await _fileSystemService.DirectoryExistsAsync(normalizedPath, cancellationToken);
        if (!exists)
        {
            ErrorMessage = $"Directory not found: {normalizedPath.DisplayPath}";
            return null;
        }

        return normalizedPath;
    }

    private async Task LoadDirectoryAsync(
        NormalizedPath path,
        string? restoreSelectionName,
        CancellationToken cancellationToken)
    {
        CancelLoading();
        CancelWatcherRefresh();
        DisposeDirectoryWatcher();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loadToken = _loadCancellation.Token;

        IsLoading = true;
        ErrorMessage = null;
        IncrementalSearchText = null;
        CurrentItem = null;
        _currentNormalizedPath = path;
        CurrentPath = path.DisplayPath;
        ResetItems();

        try
        {
            var foundRestoredSelection = false;

            await foreach (var batch in _fileSystemService.EnumerateDirectoryBatchesAsync(
                               path,
                               InitialBatchSize,
                               SubsequentBatchSize,
                               loadToken))
            {
                AddBatch(batch);

                if (!foundRestoredSelection && !string.IsNullOrEmpty(restoreSelectionName))
                {
                    var match = _sortedItems.FirstOrDefault(i =>
                        !i.IsParentEntry
                        && i.Name.Equals(restoreSelectionName, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        CurrentItem = match;
                        foundRestoredSelection = true;
                    }
                }
            }

            CurrentItem ??= _sortedItems.FirstOrDefault(static i => !i.IsParentEntry) ?? _sortedItems.FirstOrDefault();

            NotifySelectionChanged();
            OnPropertyChanged(nameof(ItemCount));
            StartWatchingDirectory(path);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Pane load canceled for {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load directory {Path}", path);
            ErrorMessage = ex.Message;
            CurrentItem = null;
            NotifySelectionChanged();
        }
        finally
        {
            if (_loadCancellation is not null && _loadCancellation.Token == loadToken)
            {
                _loadCancellation?.Dispose();
                _loadCancellation = null;
            }

            IsLoading = false;
        }
    }

    private void CancelLoading()
    {
        if (_loadCancellation is null)
        {
            return;
        }

        _loadCancellation.Cancel();
        _loadCancellation.Dispose();
        _loadCancellation = null;
    }

    private void StartWatchingDirectory(NormalizedPath path)
    {
        DisposeDirectoryWatcher();

        try
        {
            _directoryWatchSubscription = _fileSystemService.WatchDirectory(path, OnDirectoryChanged);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to start directory watcher for {Path}", path.DisplayPath);
        }
    }

    private void DisposeDirectoryWatcher()
    {
        _directoryWatchSubscription?.Dispose();
        _directoryWatchSubscription = null;
    }

    private void OnDirectoryChanged()
    {
        CancelWatcherRefresh();
        _watchRefreshDebounceCancellation = new CancellationTokenSource();
        var refreshToken = _watchRefreshDebounceCancellation.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), refreshToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            PostToUi(() => _ = RefreshFromWatcherAsync(refreshToken));
        }, CancellationToken.None);
    }

    private async Task RefreshFromWatcherAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || IsLoading || _currentNormalizedPath is null)
        {
            return;
        }

        try
        {
            await RefreshCurrentDirectoryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Background watcher refresh failed for {Path}", _currentNormalizedPath.Value.DisplayPath);
        }
    }

    private void CancelWatcherRefresh()
    {
        _watchRefreshDebounceCancellation?.Cancel();
        _watchRefreshDebounceCancellation?.Dispose();
        _watchRefreshDebounceCancellation = null;
    }

    private async Task RefreshCurrentDirectoryAsync(CancellationToken cancellationToken)
    {
        if (_currentNormalizedPath is null)
        {
            return;
        }

        var currentPath = _currentNormalizedPath.Value;
        var existingPath = await ResolveExistingDirectoryOrAncestorAsync(currentPath, cancellationToken);
        if (existingPath is null)
        {
            ErrorMessage = $"Directory not found: {currentPath.DisplayPath}";
            return;
        }

        var restoreSelectionName = CurrentItem is { IsParentEntry: false } item
            ? item.Name
            : null;

        if (!string.Equals(existingPath.Value.DisplayPath, currentPath.DisplayPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Directory {MissingPath} no longer exists. Falling back to existing ancestor {ExistingPath}.",
                currentPath.DisplayPath,
                existingPath.Value.DisplayPath);

            restoreSelectionName = GetFallbackSelectionName(currentPath, existingPath.Value);
        }

        await LoadDirectoryAsync(existingPath.Value, restoreSelectionName, cancellationToken);
    }

    private async Task<NormalizedPath?> ResolveExistingDirectoryOrAncestorAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var currentPath = path.DisplayPath;

        while (!string.IsNullOrEmpty(currentPath))
        {
            var normalizedPath = NormalizedPath.FromUserInput(currentPath);
            if (await _fileSystemService.DirectoryExistsAsync(normalizedPath, cancellationToken))
            {
                return normalizedPath;
            }

            currentPath = GetParentPath(currentPath);
        }

        return null;
    }

    private static string? GetFallbackSelectionName(NormalizedPath missingPath, NormalizedPath existingAncestorPath)
    {
        var relativePath = Path.GetRelativePath(existingAncestorPath.DisplayPath, missingPath.DisplayPath);
        if (string.IsNullOrWhiteSpace(relativePath) || string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return null;
        }

        return relativePath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
    }

    private static string? GetParentPath(string path)
    {
        var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedPath.Length == 2 && trimmedPath[1] == ':')
        {
            return null;
        }

        var parent = Path.GetDirectoryName(trimmedPath);
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    private void PostToUi(Action action)
    {
        if (_uiSynchronizationContext is null)
        {
            action();
            return;
        }

        _uiSynchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
    }

    private void ResetItems()
    {
        _sourceCache.Edit(updater =>
        {
            updater.Clear();

            if (!IsAtDriveRoot())
            {
                updater.AddOrUpdate(FileEntryViewModel.CreateParentEntry());
            }
        });

        NotifySelectionChanged();
        OnPropertyChanged(nameof(ItemCount));
    }

    private void AddBatch(IReadOnlyList<FileSystemEntryModel> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        _sourceCache.Edit(updater =>
        {
            foreach (var entry in entries)
            {
                updater.AddOrUpdate(new FileEntryViewModel(entry));
            }
        });

        OnPropertyChanged(nameof(ItemCount));
    }

    private bool IsAtDriveRoot()
    {
        if (_currentNormalizedPath is null)
        {
            return true;
        }

        var display = _currentNormalizedPath.Value.DisplayPath;
        return display.Length <= 3 && display.EndsWith(@":\", StringComparison.Ordinal);
    }
}
