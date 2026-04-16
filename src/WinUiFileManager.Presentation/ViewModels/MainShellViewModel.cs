using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.FileOperations;
using WinUiFileManager.Application.Properties;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class MainShellViewModel : ObservableObject
{
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
    private readonly ShowPropertiesCommandHandler _showPropertiesHandler;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly IDialogService _dialogService;
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly ILogger<MainShellViewModel> _logger;

    private AppSettings _currentSettings = new();

    [ObservableProperty]
    public partial FilePaneViewModel LeftPane { get; set; }

    [ObservableProperty]
    public partial FilePaneViewModel RightPane { get; set; }

    [ObservableProperty]
    public partial FilePaneViewModel ActivePane { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

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
        ShowPropertiesCommandHandler showPropertiesHandler,
        SetParallelExecutionCommandHandler setParallelExecutionHandler,
        PersistPaneStateCommandHandler persistPaneStateHandler,
        IDialogService dialogService,
        IFavouritesRepository favouritesRepository,
        ILogger<MainShellViewModel> logger,
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
        _showPropertiesHandler = showPropertiesHandler;
        _setParallelExecutionHandler = setParallelExecutionHandler;
        _persistPaneStateHandler = persistPaneStateHandler;
        _dialogService = dialogService;
        _favouritesRepository = favouritesRepository;
        _logger = logger;

        LeftPane = leftPane;
        RightPane = rightPane;
        ActivePane = leftPane;
        leftPane.IsActive = true;
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
        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
            return;

        var destination = InactivePane.CurrentNormalizedPath;
        if (destination is null)
            return;

        try
        {
            var parallelOptions = new ParallelExecutionOptions(
                _currentSettings.ParallelExecutionEnabled,
                _currentSettings.MaxDegreeOfParallelism);

            var summary = await _copyHandler.ExecuteAsync(
                items,
                destination.Value,
                CollisionPolicy.Ask,
                parallelOptions,
                new Progress<OperationProgressEvent>(),
                CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);
            await ActivePane.RefreshCommand.ExecuteAsync(null);
            await InactivePane.RefreshCommand.ExecuteAsync(null);
            FocusActivePaneRequested?.Invoke();
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
        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
            return;

        var destination = InactivePane.CurrentNormalizedPath;
        if (destination is null)
            return;

        try
        {
            var parallelOptions = new ParallelExecutionOptions(
                _currentSettings.ParallelExecutionEnabled,
                _currentSettings.MaxDegreeOfParallelism);

            var summary = await _moveHandler.ExecuteAsync(
                items,
                destination.Value,
                CollisionPolicy.Ask,
                parallelOptions,
                new Progress<OperationProgressEvent>(),
                CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);
            await ActivePane.RefreshCommand.ExecuteAsync(null);
            await InactivePane.RefreshCommand.ExecuteAsync(null);
            FocusActivePaneRequested?.Invoke();
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
        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
            return;

        try
        {
            var hasDirectories = items.Any(i => i.Kind == ItemKind.Directory);
            var confirmed = await _dialogService.ShowDeleteConfirmationAsync(
                items.Count, hasDirectories, CancellationToken.None);

            if (!confirmed)
                return;

            var summary = await _deleteHandler.ExecuteAsync(
                items,
                new Progress<OperationProgressEvent>(),
                CancellationToken.None);

            await _dialogService.ShowOperationResultAsync(summary, CancellationToken.None);
            await ActivePane.RefreshCommand.ExecuteAsync(null);
            FocusActivePaneRequested?.Invoke();
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
            return;

        try
        {
            var folderName = await _dialogService.ShowCreateFolderDialogAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(folderName))
                return;

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
            return;

        try
        {
            var newName = await _dialogService.ShowRenameDialogAsync(
                currentItem.Name, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(newName) || newName == currentItem.Name)
                return;

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
            return;

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
    private async Task ShowPropertiesAsync()
    {
        var items = ActivePane.GetSelectedEntryModels();
        if (items.Count == 0)
            return;

        try
        {
            await _showPropertiesHandler.ExecuteAsync(items, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Show properties failed");
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddFavouriteAsync()
    {
        var currentPath = ActivePane.CurrentNormalizedPath;
        if (currentPath is null)
            return;

        try
        {
            var displayName = Path.GetFileName(currentPath.Value.Value.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.IsNullOrEmpty(displayName))
                displayName = currentPath.Value.DisplayPath;

            var result = await _addFavouriteHandler.ExecuteAsync(
                displayName, currentPath.Value, CancellationToken.None);

            if (result.IsValid)
                await LoadFavouritesAsync();
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
            Favourites.Add(item);
    }
}
