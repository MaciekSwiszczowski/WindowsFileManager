using WinUiFileManager.Application.Settings;
using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    private const double MinVisibleInspectorWidth = 260d;

    private readonly ISettingsRepository _settingsRepository;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly ILogger<MainShellViewModel> _logger;
    private readonly IMessenger _messenger;

    private bool _isInspectorVisible = true;

    private AppSettings _currentSettings = new();

    [ObservableProperty]
    public partial InspectorViewModel Inspector { get; set; }

    public AppInitializationViewModel Initialization { get; }

    public PanelsViewModel Panels { get; }

    public CommandButtonsViewModel Commands { get; }

    public IMessenger Messenger => _messenger;

    public bool IsInspectorVisible => _isInspectorVisible;

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
        SetParallelExecutionCommandHandler setParallelExecutionHandler,
        PersistPaneStateCommandHandler persistPaneStateHandler,
        ILogger<MainShellViewModel> logger,
        IMessenger messenger,
        InspectorViewModel inspector,
        AppInitializationViewModel initialization,
        PanelsViewModel panels,
        CommandButtonsViewModel commands)
    {
        _messenger = messenger;
        _settingsRepository = settingsRepository;
        _setParallelExecutionHandler = setParallelExecutionHandler;
        _persistPaneStateHandler = persistPaneStateHandler;
        _logger = logger;
        Inspector = inspector;
        Initialization = initialization;
        Panels = panels;
        Commands = commands;
        _messenger.Register<ToggleInspectorRequestedMessage>(this, OnToggleInspectorLayoutMessage);
    }

    public double InspectorColumnWidth
    {
        get => _isInspectorVisible
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

    public double InspectorMinWidth => _isInspectorVisible ? MinVisibleInspectorWidth : 0d;

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
    private Task RefreshActivePaneAsync() => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        try
        {
            _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
            await Initialization.InitializeAsync(_currentSettings, CancellationToken.None);
            OnPropertyChanged(nameof(ParallelExecutionEnabled));
            Commands.IsInspectorVisible = Initialization.InspectorVisible;
            Commands.ParallelExecutionEnabled = _currentSettings.ParallelExecutionEnabled;
            InspectorWidth = _currentSettings.InspectorWidth;
            Panels.LeftPanelWidth = _currentSettings.LeftPaneWidth;
            MainWindowPlacement = _currentSettings.MainWindowPlacement;

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
                LeftPanePath: GetPanePathOrFallback(Panels.LeftPanel.FileEntries.CurrentPath, _currentSettings.LastLeftPanePath),
                RightPanePath: GetPanePathOrFallback(Panels.RightPanel.FileEntries.CurrentPath, _currentSettings.LastRightPanePath),
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

    private static NormalizedPath? GetPanePathOrFallback(string currentPath, NormalizedPath? fallback)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return fallback;
        }

        try
        {
            return NormalizedPath.FromUserInput(currentPath);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private void OnToggleInspectorLayoutMessage(object recipient, ToggleInspectorRequestedMessage message)
    {
        _isInspectorVisible = message.IsVisible;
        NotifyLayoutOnInspectorToggled();
    }

    private void NotifyLayoutOnInspectorToggled()
    {
        OnPropertyChanged(nameof(IsInspectorVisible));
        OnPropertyChanged(nameof(InspectorColumnWidth));
        OnPropertyChanged(nameof(InspectorMinWidth));
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }
}
