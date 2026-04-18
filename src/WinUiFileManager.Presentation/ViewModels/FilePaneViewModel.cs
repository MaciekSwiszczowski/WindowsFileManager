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

    private readonly SourceCache<FileEntryViewModel, string> _sourceCache = new(static x => x.UniqueKey);
    private readonly BehaviorSubject<IComparer<FileEntryViewModel>> _sortComparer;
    private readonly ReadOnlyObservableCollection<FileEntryViewModel> _sortedItems;
    private readonly IDisposable _subscription;

    private CancellationTokenSource? _loadCancellation;
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

        var restoreSelection = CurrentItem is { IsParentEntry: false } item
            ? item.Name
            : null;

        await LoadDirectoryAsync(_currentNormalizedPath.Value, restoreSelection, CancellationToken.None);
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (IsLoading)
        {
            return;
        }

        var selectable = _sortedItems.Where(static i => !i.IsParentEntry).ToList();
        var allSelected = selectable.Count > 0 && selectable.All(static i => i.IsSelected);

        foreach (var item in selectable)
        {
            item.IsSelected = !allSelected;
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
