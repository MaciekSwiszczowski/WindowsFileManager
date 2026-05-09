using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Settings;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    private const double MinVisibleInspectorWidth = 260d;

    private readonly ISettingsRepository _settingsRepository;
    private readonly RemoveFavouriteCommandHandler _removeFavouriteHandler;
    private readonly OpenFavouriteCommandHandler _openFavouriteHandler;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly IFavouritesRepository _favouritesRepository;
    private readonly ILogger<MainShellViewModel> _logger;

    private AppSettings _currentSettings = new();

    [ObservableProperty]
    public partial FileInspectorViewModel Inspector { get; set; }

    public AppInitializationViewModel Initialization { get; }

    public PanelsViewModel Panels { get; }

    public CommandButtonsViewModel Commands { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumnWidth))]
    [NotifyPropertyChangedFor(nameof(InspectorMinWidth))]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumnWidth))]
    public partial double InspectorWidth { get; set; } = 340d;

    public WindowPlacement MainWindowPlacement { get; set; } = WindowPlacement.Default;

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
        RemoveFavouriteCommandHandler removeFavouriteHandler,
        OpenFavouriteCommandHandler openFavouriteHandler,
        SetParallelExecutionCommandHandler setParallelExecutionHandler,
        PersistPaneStateCommandHandler persistPaneStateHandler,
        IFavouritesRepository favouritesRepository,
        ILogger<MainShellViewModel> logger,
        FileInspectorViewModel inspector,
        AppInitializationViewModel initialization,
        PanelsViewModel panels,
        CommandButtonsViewModel commands)
    {
        _settingsRepository = settingsRepository;
        _removeFavouriteHandler = removeFavouriteHandler;
        _openFavouriteHandler = openFavouriteHandler;
        _setParallelExecutionHandler = setParallelExecutionHandler;
        _persistPaneStateHandler = persistPaneStateHandler;
        _favouritesRepository = favouritesRepository;
        _logger = logger;
        Inspector = inspector;
        Initialization = initialization;
        Panels = panels;
        Commands = commands;
    }

    public ObservableCollection<FavouriteFolder> Favourites { get; } = [];

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

    [RelayCommand]
    private Task CopyAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task MoveAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task DeleteAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task CreateFolderAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task RenameAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task CopyFullPathAsync() => Task.CompletedTask;

    [RelayCommand]
    private Task AddFavouriteAsync() => Task.CompletedTask;

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
            await _openFavouriteHandler.ExecuteAsync(id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open favourite failed");
        }
    }

    [RelayCommand]
    private Task RefreshActivePaneAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        try
        {
            _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
            await Initialization.InitializeAsync();
            OnPropertyChanged(nameof(ParallelExecutionEnabled));
            IsInspectorVisible = _currentSettings.InspectorVisible;
            Commands.IsInspectorVisible = IsInspectorVisible;
            Commands.ParallelExecutionEnabled = _currentSettings.ParallelExecutionEnabled;
            InspectorWidth = _currentSettings.InspectorWidth;
            Panels.LeftPanelWidth = _currentSettings.LeftPaneWidth;
            MainWindowPlacement = _currentSettings.MainWindowPlacement;

            await LoadFavouritesAsync();

            Panels.SetActivePanel(string.IsNullOrWhiteSpace(_currentSettings.LastActivePane)
                ? "Left"
                : _currentSettings.LastActivePane);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialization failed");
        }
    }

    public async Task PersistStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PersistPaneStateRequest(
                LeftPanePath: _currentSettings.LastLeftPanePath,
                RightPanePath: _currentSettings.LastRightPanePath,
                ActivePane: Panels.ActivePanelIdentity,
                InspectorVisible: IsInspectorVisible,
                InspectorWidth: InspectorWidth,
                LeftPaneWidth: Panels.LeftPanelWidth,
                LeftPaneColumns: _currentSettings.LeftPaneColumns,
                RightPaneColumns: _currentSettings.RightPaneColumns,
                LeftPaneSort: _currentSettings.LeftPaneSort,
                RightPaneSort: _currentSettings.RightPaneSort,
                MainWindowPlacement: MainWindowPlacement);

            await _persistPaneStateHandler.ExecuteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persisting pane state failed");
        }
    }

    private async Task LoadFavouritesAsync()
    {
        var items = await _favouritesRepository.GetAllAsync(CancellationToken.None);
        Favourites.Clear();
        foreach (var item in items)
        {
            Favourites.Add(item);
        }

        Commands.SetFavourites(Favourites);
    }

    [RelayCommand]
    private void ToggleInspector()
    {
        IsInspectorVisible = !IsInspectorVisible;
    }

    public void Dispose()
    {
    }
}
