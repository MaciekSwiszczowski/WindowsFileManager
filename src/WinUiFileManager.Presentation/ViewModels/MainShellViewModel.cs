using WinUiFileManager.Application.Startup;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Root view model for the main application shell window. Composes the top-level child view models
/// (<see cref="Inspector"/>, <see cref="Panels"/>, <see cref="Commands"/>, <see cref="Initialization"/>),
/// owns shell-level layout state (inspector visibility/width, window placement), and brokers persisted
/// startup settings into those children. Sits at the top of the Presentation view-model graph.
/// </summary>
/// <remarks>
/// <para>
/// Lifetime: created once per shell window on the UI/dispatcher thread (enforced in the constructor).
/// </para>
/// <para>
/// Messaging: registers two recipients against the app-wide <see cref="IMessenger"/>
/// (<see cref="AppStartupDataLoadedMessage"/> and <see cref="ToggleInspectorRequestedMessage"/>).
/// Because the app messenger is <c>StrongReferenceMessenger.Default</c> (see AGENTS.md §4), those
/// registrations root this instance until <see cref="Dispose"/> calls <see cref="IMessenger.UnregisterAll(object)"/>.
/// </para>
/// <para>
/// Known leak hazard (documented, not fixed): <see cref="Dispose"/> only unregisters this instance from
/// the messenger — it does <b>not</b> cascade <see cref="IDisposable.Dispose"/> to the child view models
/// (<see cref="Panels"/>, <see cref="Commands"/>, <see cref="Inspector"/>), and there is no guarantee that
/// <see cref="Dispose"/> itself is reliably invoked from the window-close lifecycle. Any registrations held
/// by the children therefore outlive this object. See AGENTS.md §4/§5.
/// </para>
/// </remarks>
public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    /// <summary>Lower bound (in DIPs) applied to the inspector column whenever the inspector is visible.</summary>
    private const double MinVisibleInspectorWidth = 260d;

    private readonly ISettingsRepository _settingsRepository;
    private readonly SetParallelExecutionCommandHandler _setParallelExecutionHandler;
    private readonly PersistPaneStateCommandHandler _persistPaneStateHandler;
    private readonly ILogger<MainShellViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly UiDispatcherQueue _dispatcherQueue;

    private bool _isInspectorVisible = true;

    /// <summary>Last settings snapshot loaded from / about to be persisted to the settings repository.</summary>
    private AppSettings _currentSettings = new();

    /// <summary>Inspector panel view model bound to the right-hand diagnostics pane.</summary>
    [ObservableProperty]
    public partial InspectorViewModel Inspector { get; set; }

    /// <summary>Startup/initialization state (volumes, initial inspector visibility) shared with the panes.</summary>
    public AppInitializationViewModel Initialization { get; }

    /// <summary>The dual-pane container view model (left/right <see cref="PanelViewModel"/>s).</summary>
    public PanelsViewModel Panels { get; }

    /// <summary>Command-bar buttons view model (copy/move/delete/etc.).</summary>
    public CommandButtonsViewModel Commands { get; }

    /// <summary>Exposes the app-wide messenger to the view for behaviors/bindings that need it.</summary>
    public IMessenger Messenger => _messenger;

    /// <summary>Whether the inspector pane is currently shown. Toggled via <see cref="ToggleInspectorRequestedMessage"/>.</summary>
    public bool IsInspectorVisible => _isInspectorVisible;

    /// <summary>
    /// Persisted desired inspector width (DIPs). Changing it re-evaluates <see cref="InspectorColumnWidth"/>
    /// via <see cref="NotifyPropertyChangedForAttribute"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumnWidth))]
    public partial double InspectorWidth { get; set; } = 340d;

    /// <summary>Main window placement (position/size/state) restored from and persisted to settings.</summary>
    [ObservableProperty]
    public partial WindowPlacement MainWindowPlacement { get; set; } = WindowPlacement.Default;

    /// <summary>
    /// Mirrors the parallel-execution setting. The setter is intentionally fire-and-forget: it delegates to
    /// <see cref="OnParallelExecutionEnabledChanged"/> only when the value actually changes, so the toggle
    /// does not write settings on no-op assignments.
    /// </summary>
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

    /// <summary>
    /// Applies a parallel-execution toggle through the command handler, then reloads settings to reflect the
    /// authoritative persisted value.
    /// </summary>
    /// <remarks>
    /// <c>async void</c> on purpose: this is invoked from a property setter (UI affinity) and there is no caller
    /// to await it. The top-level <c>try/catch</c> is mandatory for <c>async void</c> (AGENTS.md §6) so a failed
    /// handler/settings write logs instead of crashing on an unobserved exception.
    /// </remarks>
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

    /// <summary>
    /// Creates the shell view model and registers its messenger recipients. Must run on a dispatcher (UI) thread:
    /// the constructor captures <see cref="UiDispatcherQueue.GetForCurrentThread"/> and throws if none exists,
    /// because startup-data application and layout updates must be marshalled back to this queue.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when constructed off a dispatcher thread.</exception>
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
        _dispatcherQueue = UiDispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException($"{nameof(MainShellViewModel)} must be created on a dispatcher thread.");
        _messenger.Register<AppStartupDataLoadedMessage>(this, OnAppStartupDataLoaded);
        _messenger.Register<ToggleInspectorRequestedMessage>(this, OnToggleInspectorLayoutMessage);
    }

    /// <summary>
    /// Effective grid-column width for the inspector: collapses to <c>0</c> when hidden, otherwise clamps to at
    /// least <see cref="MinVisibleInspectorWidth"/>. The setter is what the splitter binds to; non-positive
    /// values (a collapsed drag) are ignored so the persisted <see cref="InspectorWidth"/> is preserved.
    /// </summary>
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

    /// <summary>Minimum grid-column width for the inspector: the visible floor when shown, else <c>0</c>.</summary>
    public double InspectorMinWidth => _isInspectorVisible ? MinVisibleInspectorWidth : 0d;

    /// <summary>
    /// Records a width produced by the splitter/layout pass. Non-positive widths are ignored; positive widths are
    /// clamped to <see cref="MinVisibleInspectorWidth"/> before becoming the persisted <see cref="InspectorWidth"/>.
    /// </summary>
    public void UpdateInspectorWidthFromLayout(double width)
    {
        if (width <= 0d)
        {
            return;
        }

        InspectorWidth = Math.Max(width, MinVisibleInspectorWidth);
    }

    // The following [RelayCommand] methods are intentional stubs (Task.CompletedTask): the command-bar bindings
    // exist so the shell UI can wire/enable these actions, but the bulk file-operation behaviors behind them are
    // not implemented yet (favourites and bulk operations are the remaining large features per AGENTS.md §1).

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

    /// <summary>
    /// Handles <see cref="AppStartupDataLoadedMessage"/>. The message may arrive on a background thread, so this
    /// marshals <see cref="ApplyStartupData"/> onto the captured dispatcher when off-thread; failure to enqueue is
    /// logged rather than thrown.
    /// </summary>
    private void OnAppStartupDataLoaded(object recipient, AppStartupDataLoadedMessage message)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            ApplyStartupData(message.StartupData);
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(() => ApplyStartupData(message.StartupData)))
        {
            _logger.LogError("Failed to enqueue startup data application on the UI dispatcher.");
        }
    }

    /// <summary>
    /// Pushes the loaded startup data into the child view models and shell layout (settings, volumes, inspector
    /// visibility/width, pane widths, window placement, active pane). UI-thread affine — invoked only via
    /// <see cref="OnAppStartupDataLoaded"/> on the dispatcher. Failures are logged, not propagated.
    /// </summary>
    private void ApplyStartupData(AppStartupData startupData)
    {
        try
        {
            _currentSettings = startupData.Settings;
            Initialization.Initialize(_currentSettings, startupData.NtfsVolumes);
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

    /// <summary>
    /// Builds a <see cref="PersistPaneStateRequest"/> from the current shell/pane state and writes it via the
    /// persist handler. Intended to be called on shutdown/teardown. Exceptions are caught and logged so a failed
    /// save never blocks shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the persist I/O.</param>
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

    /// <summary>
    /// Converts a pane's current path string to a <see cref="NormalizedPath"/>, falling back to the previously
    /// persisted path when the current path is blank or fails to parse (so a transient/invalid path does not
    /// overwrite a good persisted value).
    /// </summary>
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

    /// <summary>
    /// Handles <see cref="ToggleInspectorRequestedMessage"/> by updating visibility and re-raising the dependent
    /// layout properties so the inspector column collapses/expands.
    /// </summary>
    private void OnToggleInspectorLayoutMessage(object recipient, ToggleInspectorRequestedMessage message)
    {
        _isInspectorVisible = message.IsVisible;
        NotifyLayoutOnInspectorToggled();
    }

    /// <summary>Raises change notifications for the visibility-derived layout properties as a group.</summary>
    private void NotifyLayoutOnInspectorToggled()
    {
        OnPropertyChanged(nameof(IsInspectorVisible));
        OnPropertyChanged(nameof(InspectorColumnWidth));
        OnPropertyChanged(nameof(InspectorMinWidth));
    }

    /// <summary>
    /// Unregisters this instance from the app messenger. See the type <c>&lt;remarks&gt;</c>: this does
    /// <b>not</b> dispose the child view models and is not guaranteed to be invoked on window close — a known,
    /// documented latent leak, not addressed here.
    /// </summary>
    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }
}
