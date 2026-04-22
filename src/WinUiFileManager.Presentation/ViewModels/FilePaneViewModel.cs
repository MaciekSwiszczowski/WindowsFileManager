using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FilePaneViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan LoaderBufferWindow = TimeSpan.FromMilliseconds(50);
    private const int LoaderBufferCount = 500;

    // Watcher events are coalesced into batches on the background scheduler so
    // bursts (e.g. copying 10 000 files into the watched folder) collapse into
    // a handful of UI commits rather than one commit per file.
    private static readonly TimeSpan WatcherBufferWindow = TimeSpan.FromMilliseconds(100);

    private readonly OpenEntryCommandHandler _openEntryHandler;
    private readonly RenameEntryCommandHandler _renameHandler;
    private readonly IFileSystemService _fileSystemService;
    private readonly IDirectoryChangeStream _directoryChangeStream;
    private readonly ISchedulerProvider _schedulers;
    private readonly INtfsVolumePolicyService _volumePolicyService;
    private readonly IPathNormalizationService _pathNormalizationService;
    private readonly ILogger<FilePaneViewModel> _logger;

    private readonly SourceCache<FileEntryViewModel, string> _sourceCache = new(static x => x.UniqueKey);
    private readonly BehaviorSubject<IComparer<FileEntryViewModel>> _sortComparer;
    private readonly ReadOnlyObservableCollection<FileEntryViewModel> _sortedItems;
    private readonly IDisposable _subscription;

    private CancellationTokenSource? _loadCancellation;
    private IDisposable? _directoryWatchSubscription;
    private NormalizedPath? _currentNormalizedPath;
    private FileEntryViewModel? _activeEditingEntry;

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
    [NotifyPropertyChangedFor(nameof(SortState))]
    public partial SortColumn SortBy { get; set; } = SortColumn.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortState))]
    public partial bool SortAscending { get; set; } = true;

    [ObservableProperty]
    public partial PaneColumnLayout ColumnLayout { get; set; } = PaneColumnLayout.Default;

    public SortState SortState => new(SortBy, SortAscending);

    public event EventHandler<FileEntryViewModel>? RenameRequested;

    public ObservableCollection<VolumeInfo> AvailableDrives { get; } = [];

    [ObservableProperty]
    public partial VolumeInfo? SelectedDrive { get; set; }

    public FilePaneViewModel(
        OpenEntryCommandHandler openEntryHandler,
        RenameEntryCommandHandler renameHandler,
        IFileSystemService fileSystemService,
        IDirectoryChangeStream directoryChangeStream,
        ISchedulerProvider schedulers,
        INtfsVolumePolicyService volumePolicyService,
        IPathNormalizationService pathNormalizationService,
        ILogger<FilePaneViewModel> logger)
    {
        _openEntryHandler = openEntryHandler;
        _renameHandler = renameHandler;
        _fileSystemService = fileSystemService;
        _directoryChangeStream = directoryChangeStream;
        _schedulers = schedulers;
        _volumePolicyService = volumePolicyService;
        _pathNormalizationService = pathNormalizationService;
        _logger = logger;

        _sortComparer = new BehaviorSubject<IComparer<FileEntryViewModel>>(
            new FileEntryComparer(SortColumn.Name, true));

        _subscription = _sourceCache.Connect()
            .ObserveOn(_schedulers.MainThread)
            .SortAndBind(out _sortedItems, _sortComparer.AsObservable())
            .Subscribe();
    }

    public PaneId PaneId { get; set; }

    public ReadOnlyObservableCollection<FileEntryViewModel> Items => _sortedItems;

    public int ItemCount => _sortedItems.Count;

    public int SelectedCount => _sortedItems.Count(static i => i.IsSelected);

    public NormalizedPath? CurrentNormalizedPath => _currentNormalizedPath;

    public bool IsInteractive => !IsLoading;

    public FileEntryViewModel? ActiveEditingEntry => _activeEditingEntry;

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

    public void ApplySortState(SortState state)
    {
        SortBy = state.Column;
        SortAscending = state.Ascending;
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

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
    }

    public void BeginRenameCurrent()
    {
        if (IsLoading)
        {
            return;
        }

        var current = CurrentItem;
        if (current is null || current.IsParentEntry)
        {
            return;
        }

        if (_activeEditingEntry is not null && !ReferenceEquals(_activeEditingEntry, current))
        {
            CancelRename(_activeEditingEntry);
        }

        ErrorMessage = null;
        current.EditBuffer = current.Name;
        current.IsEditing = true;
        _activeEditingEntry = current;
        RenameRequested?.Invoke(this, current);
    }

    public async Task<bool> CommitRenameAsync(
        FileEntryViewModel entry,
        string? candidateName,
        CancellationToken ct)
    {
        if (!ReferenceEquals(_activeEditingEntry, entry) || !entry.IsEditing)
        {
            return false;
        }

        var newName = (candidateName ?? entry.EditBuffer).Trim();
        entry.EditBuffer = candidateName ?? entry.EditBuffer;
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, entry.Name, StringComparison.Ordinal))
        {
            CancelRename(entry);
            return true;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        try
        {
            var summary = await _renameHandler.ExecuteAsync(entry.Model, newName, ct);
            if (summary.FailedCount == 0 && summary.Status == OperationStatus.Succeeded)
            {
                entry.IsEditing = false;
                entry.EditBuffer = string.Empty;
                _activeEditingEntry = null;
                return true;
            }

            _logger.LogDebug(
                "Inline rename rejected for {Path}: {Message}",
                entry.Model.FullPath.DisplayPath,
                summary.Message
                ?? summary.ItemResults.FirstOrDefault(static result => !result.Succeeded)?.Error?.Message
                ?? "Rename failed.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rename failed");
            return false;
        }
    }

    public void CancelRename(FileEntryViewModel entry)
    {
        entry.IsEditing = false;
        entry.EditBuffer = string.Empty;

        if (ReferenceEquals(_activeEditingEntry, entry))
        {
            _activeEditingEntry = null;
        }
    }

    partial void OnCurrentItemChanged(FileEntryViewModel? value)
    {
        if (_activeEditingEntry is not null && !ReferenceEquals(_activeEditingEntry, value))
        {
            CancelRename(_activeEditingEntry);
        }
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
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
            await _fileSystemService
                .ObserveDirectoryEntries(path, _schedulers.Background, loadToken)
                .Select(static model => new FileEntryViewModel(model))
                .Buffer(LoaderBufferWindow, LoaderBufferCount, _schedulers.Background)
                .Where(static batch => batch.Count > 0)
                .Do(batch => _sourceCache.AddOrUpdate(batch))
                .DefaultIfEmpty(Array.Empty<FileEntryViewModel>())
                .LastAsync()
                .ObserveOn(_schedulers.MainThread)
                .ToTask(loadToken);

            if (!string.IsNullOrEmpty(restoreSelectionName))
            {
                var match = _sortedItems.FirstOrDefault(i =>
                    !i.IsParentEntry
                    && i.Name.Equals(restoreSelectionName, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    CurrentItem = match;
                }
            }

            CurrentItem ??= _sortedItems.FirstOrDefault(static i => !i.IsParentEntry) ?? _sortedItems.FirstOrDefault();

            NotifySelectionChanged();
            OnPropertyChanged(nameof(ItemCount));
            StartWatchingDirectory(path);
        }
        catch (OperationCanceledException)
        {
            FilePaneViewModelLog.PaneLoadCanceled(_logger, path.DisplayPath);
        }
        catch (Exception ex)
        {
            FilePaneViewModelLog.DirectoryLoadFailed(_logger, ex, path.DisplayPath);
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
        var watchedPath = path;

        try
        {
            _directoryWatchSubscription = _directoryChangeStream
                .Watch(path)
                .Buffer(WatcherBufferWindow, _schedulers.Background)
                .Where(static batch => batch.Count > 0)
                .Select(batch => BuildWatcherBatch(watchedPath, batch))
                .ObserveOn(_schedulers.MainThread)
                .Subscribe(
                    batch => ApplyWatcherBatch(watchedPath, batch),
                    ex => FilePaneViewModelLog.DirectoryWatcherPipelineFailed(_logger, ex, watchedPath.DisplayPath));
        }
        catch (Exception ex)
        {
            FilePaneViewModelLog.DirectoryWatcherStartFailed(_logger, ex, path.DisplayPath);
        }
    }

    private void DisposeDirectoryWatcher()
    {
        _directoryWatchSubscription?.Dispose();
        _directoryWatchSubscription = null;
    }

    // Runs on the background scheduler. Resolves metadata for
    // Created/Changed/Renamed events here so the UI thread only performs the
    // final SourceCache edit + TableView bind.
    private WatcherBatch BuildWatcherBatch(NormalizedPath watchedPath, IList<DirectoryChange> changes)
    {
        var needsFullRescan = false;
        var perPath = new Dictionary<string, DirectoryChange>(StringComparer.OrdinalIgnoreCase);
        var removedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var renamedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in changes)
        {
            if (change.Kind == DirectoryChangeKind.Invalidated)
            {
                needsFullRescan = true;
                continue;
            }

            if (change is { Kind: DirectoryChangeKind.Renamed, OldPath: { } oldPath })
            {
                removedPaths.Add(oldPath.DisplayPath);
                perPath.Remove(oldPath.DisplayPath);
                renamedPaths[oldPath.DisplayPath] = change.Path.DisplayPath;
            }

            if (change.Kind == DirectoryChangeKind.Deleted)
            {
                removedPaths.Add(change.Path.DisplayPath);
                perPath.Remove(change.Path.DisplayPath);
                continue;
            }

            perPath[change.Path.DisplayPath] = change;
            removedPaths.Remove(change.Path.DisplayPath);
        }

        if (needsFullRescan)
        {
            return WatcherBatch.FullRescan;
        }

        var added = new List<FileEntryViewModel>(perPath.Count);
        foreach (var change in perPath.Values)
        {
            if (!IsDirectChildOf(watchedPath, change.Path))
            {
                continue;
            }

            var entry = ResolveEntryViewModel(change.Path);
            if (entry is not null)
            {
                added.Add(entry);
            }
            else
            {
                removedPaths.Add(change.Path.DisplayPath);
            }
        }

        return new WatcherBatch(false, added, removedPaths, renamedPaths);
    }

    // Runs on the UI thread. One SourceCache edit per batch → one bound
    // ChangeSet → one TableView update regardless of event count.
    private void ApplyWatcherBatch(NormalizedPath watchedPath, WatcherBatch batch)
    {
        if (batch.NeedsFullRescan)
        {
            FilePaneViewModelLog.DirectoryWatcherRequestedFullRescan(_logger, watchedPath.DisplayPath);
            _ = RefreshCurrentDirectoryAsync(CancellationToken.None);
            return;
        }

        if (batch.RemovedPaths.Count == 0 && batch.AddedOrUpdated.Count == 0)
        {
            return;
        }

        var currentItemPath = CurrentItem is { IsParentEntry: false } currentItem
            ? currentItem.Model.FullPath.DisplayPath
            : null;
        var selectedPaths = _sortedItems
            .Where(static item => item is { IsSelected: true, IsParentEntry: false })
            .Select(static item => item.Model.FullPath.DisplayPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        FileEntryViewModel? replacementCurrentItem = null;

        _sourceCache.Edit(updater =>
        {
            if (batch.RemovedPaths.Count > 0)
            {
                updater.Remove(batch.RemovedPaths);
            }

            if (batch.AddedOrUpdated.Count > 0)
            {
                updater.AddOrUpdate(batch.AddedOrUpdated);
            }
        });

        if (batch.RenamedPaths.Count > 0)
        {
            if (currentItemPath is not null
                && batch.RenamedPaths.TryGetValue(currentItemPath, out var renamedCurrentItemPath))
            {
                currentItemPath = renamedCurrentItemPath;
                selectedPaths.Add(renamedCurrentItemPath);
                replacementCurrentItem = batch.AddedOrUpdated.FirstOrDefault(item =>
                    string.Equals(item.Model.FullPath.DisplayPath, renamedCurrentItemPath, StringComparison.OrdinalIgnoreCase));
            }

            selectedPaths = selectedPaths
                .Select(path => batch.RenamedPaths.TryGetValue(path, out var renamedPath) ? renamedPath : path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var item in _sortedItems.Where(static item => !item.IsParentEntry))
        {
            item.IsSelected = selectedPaths.Contains(item.Model.FullPath.DisplayPath);
        }

        if (replacementCurrentItem is not null)
        {
            replacementCurrentItem.IsSelected = selectedPaths.Contains(replacementCurrentItem.Model.FullPath.DisplayPath);
        }

        if (replacementCurrentItem is not null)
        {
            CurrentItem = replacementCurrentItem;
        }
        else if (currentItemPath is not null)
        {
            CurrentItem = _sortedItems.FirstOrDefault(item =>
                !item.IsParentEntry
                && string.Equals(item.Model.FullPath.DisplayPath, currentItemPath, StringComparison.OrdinalIgnoreCase))
                ?? CurrentItem;
        }

        NotifySelectionChanged();
        OnPropertyChanged(nameof(ItemCount));
    }

    private FileEntryViewModel? ResolveEntryViewModel(NormalizedPath path)
    {
        try
        {
            var model = _fileSystemService
                .GetEntryAsync(path, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return model is null ? null : new FileEntryViewModel(model);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve metadata for {Path}", path.DisplayPath);
            return null;
        }
    }

    private static bool IsDirectChildOf(NormalizedPath parent, NormalizedPath child)
    {
        var parentDisplay = parent.DisplayPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var childDir = Path.GetDirectoryName(child.DisplayPath);
        if (string.IsNullOrEmpty(childDir))
        {
            return false;
        }

        var normalizedChildDir = childDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(parentDisplay, normalizedChildDir, StringComparison.OrdinalIgnoreCase);
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
            FilePaneViewModelLog.DirectoryFallbackToExistingAncestor(
                _logger,
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
        if (trimmedPath is
            [
                _, ':'
            ])
        {
            return null;
        }

        var parent = Path.GetDirectoryName(trimmedPath);
        return string.IsNullOrEmpty(parent) ? null : parent;
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

    private bool IsAtDriveRoot()
    {
        if (_currentNormalizedPath is null)
        {
            return true;
        }

        var display = _currentNormalizedPath.Value.DisplayPath;
        return display.Length <= 3 && display.EndsWith(@":\", StringComparison.Ordinal);
    }

    private sealed class WatcherBatch
    {
        public static readonly WatcherBatch FullRescan = new(true, [], [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public WatcherBatch(
            bool needsFullRescan,
            IReadOnlyList<FileEntryViewModel> addedOrUpdated,
            IReadOnlyCollection<string> removedPaths,
            IReadOnlyDictionary<string, string> renamedPaths)
        {
            NeedsFullRescan = needsFullRescan;
            AddedOrUpdated = addedOrUpdated;
            RemovedPaths = removedPaths;
            RenamedPaths = renamedPaths;
        }

        public bool NeedsFullRescan { get; }

        public IReadOnlyList<FileEntryViewModel> AddedOrUpdated { get; }

        public IReadOnlyCollection<string> RemovedPaths { get; }

        public IReadOnlyDictionary<string, string> RenamedPaths { get; }
    }
}
