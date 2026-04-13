using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FilePaneViewModel : ObservableObject, IDisposable
{
    private readonly OpenEntryCommandHandler _openEntryHandler;
    private readonly NavigateUpCommandHandler _navigateUpHandler;
    private readonly GoToPathCommandHandler _goToPathHandler;
    private readonly RefreshPaneCommandHandler _refreshPaneHandler;
    private readonly INtfsVolumePolicyService _volumePolicyService;
    private readonly ILogger<FilePaneViewModel> _logger;

    private readonly SourceCache<FileEntryViewModel, string> _sourceCache = new(x => x.UniqueKey);
    private readonly BehaviorSubject<IComparer<FileEntryViewModel>> _sortComparer;
    private readonly ReadOnlyObservableCollection<FileEntryViewModel> _sortedItems;
    private readonly IDisposable _subscription;

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
    public partial string? IncrementalSearchText { get; set; }

    [ObservableProperty]
    public partial SortColumn SortBy { get; set; } = SortColumn.Name;

    [ObservableProperty]
    public partial bool SortAscending { get; set; } = true;

    public ObservableCollection<VolumeInfo> AvailableDrives { get; } = [];

    [ObservableProperty]
    public partial VolumeInfo? SelectedDrive { get; set; }

    public FilePaneViewModel(
        OpenEntryCommandHandler openEntryHandler,
        NavigateUpCommandHandler navigateUpHandler,
        GoToPathCommandHandler goToPathHandler,
        RefreshPaneCommandHandler refreshPaneHandler,
        INtfsVolumePolicyService volumePolicyService,
        ILogger<FilePaneViewModel> logger)
    {
        _openEntryHandler = openEntryHandler;
        _navigateUpHandler = navigateUpHandler;
        _goToPathHandler = goToPathHandler;
        _refreshPaneHandler = refreshPaneHandler;
        _volumePolicyService = volumePolicyService;
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

    public int SelectedCount => _sortedItems.Count(i => i.IsSelected);

    public NormalizedPath? CurrentNormalizedPath => _currentNormalizedPath;

    public void SetSort(SortColumn column)
    {
        if (SortBy == column)
            SortAscending = !SortAscending;
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
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _goToPathHandler.ExecuteAsync(path, CancellationToken.None);

            if (result.Success && result.Entries is not null && result.Path is not null)
            {
                _currentNormalizedPath = result.Path;
                CurrentPath = result.Path.Value.DisplayPath;
                PopulateItems(result.Entries);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Navigation failed.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Path}", path);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateIntoAsync()
    {
        if (CurrentItem is null)
            return;

        if (CurrentItem.IsParentEntry)
        {
            await NavigateUpAsync();
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var isDirectory = CurrentItem.IsDirectory;
            var targetPath = CurrentItem.Model.FullPath;

            var entries = await _openEntryHandler.ExecuteAsync(
                targetPath, CancellationToken.None);

            if (isDirectory)
            {
                _currentNormalizedPath = targetPath;
                CurrentPath = targetPath.DisplayPath;
                PopulateItems(entries);
                ClearSelection(); // Selection cleared on successful navigation into folder
            }
            // If it's a file, OpenEntryCommandHandler handles opening it and returns empty list.
            // We don't want to change the current view or selection.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate into {Path}", CurrentItem.FullPath);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (_currentNormalizedPath is null)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var oldPath = _currentNormalizedPath.Value;
            var result = await _navigateUpHandler.ExecuteAsync(
                oldPath, CancellationToken.None);

            if (result is not null)
            {
                _currentNormalizedPath = result.Value.Path;
                CurrentPath = result.Value.Path.DisplayPath;
                PopulateItems(result.Value.Entries);
                ClearSelection(); // Selection cleared on successful navigation up

                // Try to land on the directory we just came from
                var folderName = Path.GetFileName(oldPath.DisplayPath.TrimEnd(Path.DirectorySeparatorChar));
                if (!string.IsNullOrEmpty(folderName))
                {
                    var previousFolder = _sortedItems.FirstOrDefault(i => i.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase));
                    if (previousFolder != null)
                    {
                        CurrentItem = previousFolder;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate up from {Path}", CurrentPath);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_currentNormalizedPath is null)
            return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var entries = await _refreshPaneHandler.ExecuteAsync(
                _currentNormalizedPath.Value, CancellationToken.None);
            PopulateItems(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh {Path}", CurrentPath);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        var allSelected = _sortedItems.All(i => i.IsSelected);
        foreach (var item in _sortedItems)
            item.IsSelected = !allSelected;

        NotifySelectionChanged();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in _sortedItems)
            item.IsSelected = false;

        NotifySelectionChanged();
    }

    public void ToggleSelection(FileEntryViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        NotifySelectionChanged();
    }

    public void HandleIncrementalSearch(char c)
    {
        IncrementalSearchText = (IncrementalSearchText ?? string.Empty) + c;

        var match = _sortedItems.FirstOrDefault(i =>
            i.Name.StartsWith(IncrementalSearchText, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            CurrentItem = match;
    }

    public void ClearIncrementalSearch()
    {
        IncrementalSearchText = null;
    }

    public void BackspaceIncrementalSearch()
    {
        if (string.IsNullOrEmpty(IncrementalSearchText))
            return;

        IncrementalSearchText = IncrementalSearchText[..^1];
        if (string.IsNullOrEmpty(IncrementalSearchText))
        {
            ClearIncrementalSearch();
            return;
        }

        var match = _sortedItems.FirstOrDefault(i =>
            i.Name.StartsWith(IncrementalSearchText, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            CurrentItem = match;
    }

    public async Task LoadDrivesAsync()
    {
        var drives = await _volumePolicyService.GetNtfsVolumesAsync(CancellationToken.None);
        AvailableDrives.Clear();
        foreach (var drive in drives)
            AvailableDrives.Add(drive);
    }

    partial void OnSelectedDriveChanged(VolumeInfo? value)
    {
        if (value is not null)
        {
            _ = NavigateToCommand.ExecuteAsync(value.RootPath.DisplayPath);
        }
    }

    public IReadOnlyList<FileSystemEntryModel> GetSelectedEntryModels()
    {
        var selected = _sortedItems
            .Where(i => i.IsSelected && !i.IsParentEntry)
            .Select(i => i.Model)
            .ToList();

        if (selected.Count > 0)
            return selected;

        if (CurrentItem is { IsParentEntry: false })
            return [CurrentItem.Model];

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

    private void PopulateItems(IReadOnlyList<FileSystemEntryModel> entries)
    {
        _sourceCache.Edit(updater =>
        {
            updater.Clear();

            if (!IsAtDriveRoot())
            {
                var parentEntry = FileEntryViewModel.CreateParentEntry();
                updater.AddOrUpdate(parentEntry);
            }

            foreach (var entry in entries)
                updater.AddOrUpdate(new FileEntryViewModel(entry));
        });

        CurrentItem = _sortedItems.Count > 0 ? _sortedItems[0] : null;
        NotifySelectionChanged();
        OnPropertyChanged(nameof(ItemCount));
    }

    private bool IsAtDriveRoot()
    {
        if (_currentNormalizedPath is null)
            return true;

        var display = _currentNormalizedPath.Value.DisplayPath;
        return display.Length <= 3 && display.EndsWith(@":\", StringComparison.Ordinal);
    }

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _sortComparer.Dispose();
        _sourceCache.Dispose();
    }
}
