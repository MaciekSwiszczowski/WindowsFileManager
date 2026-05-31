namespace WinUiFileManager.App.Windows;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Presentation.MessageLogging;
using Presentation.Services;
using Presentation.ViewModels;

/// <summary>
/// Code-behind for the application's single top-level shell window. Owns the window-level helpers
/// (placement tracking, theming), wires the shell view to its view model, and drives the
/// persist-then-close shutdown sequence. App layer; entirely UI-thread affine.
/// </summary>
/// <remarks>
/// Idempotency: both <see cref="Initialize"/> and the load handler are guarded so the framework may
/// raise their triggers more than once without re-running setup. Shutdown persistence is driven from
/// <see cref="OnAppWindowClosing"/> rather than container disposal, because the DI container is never
/// disposed (see <see cref="App"/> / AGENTS.md §5).
/// </remarks>
public sealed partial class MainShellWindow
{
    /// <summary>
    /// Builds the window chrome: sets the icon, hooks the closing handler, and constructs the placement
    /// and theme managers (defaulting to dark theme).
    /// </summary>
    /// <remarks>
    /// The view model is intentionally not required here; it arrives later via <see cref="Initialize"/>
    /// so the window can be DI-resolved before the VM graph is touched. Subscriptions made here
    /// (<c>AppWindow.Closing</c>, <c>ShellView.Loaded</c>) live for the window's lifetime.
    /// </remarks>
    public MainShellWindow()
    {
        InitializeComponent();

        var appWindow = AppWindow;
        appWindow.SetIcon("Assets\\app-icon.ico");
        appWindow.Closing += OnAppWindowClosing;
        _windowManager = new WindowManager(this, appWindow);
        _themeManager = new WindowThemeManager(this, appWindow);
        _themeManager.Apply(ElementTheme.Dark);

        ShellView.Loaded += OnShellViewLoaded;
    }

    private bool _loaded;
    private bool _initialized;
    private MainShellViewModel? _viewModel;
    private bool _statePersisted;
    private readonly WindowManager _windowManager;
    private readonly WindowThemeManager _themeManager;

    /// <summary>
    /// Connects the window to its view model and the shell view: applies placement tracking, wires the
    /// theme toggle, and forwards the (debug-only) message-log opener.
    /// </summary>
    /// <param name="viewModel">The shell view model to bind. Stored for use during close-time persistence.</param>
    /// <remarks>
    /// Guarded by <see cref="_initialized"/> so repeated calls are no-ops. Must run on the UI thread.
    /// </remarks>
    public void Initialize(MainShellViewModel viewModel)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _viewModel = viewModel;

        _windowManager.Initialize(viewModel);
        ShellView.ToggleThemeAction = _themeManager.ToggleTheme;
        Action? openMessageLogWindow = null;
        ConfigureMessageLogWindowAction(ref openMessageLogWindow);
        ShellView.Initialize(viewModel, openMessageLogWindow);
    }

    /// <summary>
    /// On first shell-view load, attaches the <see cref="DialogService"/> to this window's
    /// <see cref="Microsoft.UI.Xaml.UIElement.XamlRoot"/> and dispatcher so dialogs can be shown.
    /// </summary>
    /// <remarks>
    /// Guarded by <see cref="_loaded"/>: <c>Loaded</c> can fire more than once over a window's life. The
    /// <c>XamlRoot</c> is only valid once the view is loaded, which is why attaching happens here rather
    /// than in the constructor. The dialog service is pulled from the global container because this
    /// framework-constructed view cannot use constructor injection.
    /// </remarks>
    private void OnShellViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var services = App.Services;
        var dialogService = services.GetRequiredService<DialogService>();
        dialogService.Attach(ShellView.XamlRoot, DispatcherQueue);
    }

    /// <summary>
    /// Closing handler that persists window/pane state before actually letting the window close.
    /// </summary>
    /// <remarks>
    /// This is an <c>async void</c> handler — permitted because it is a genuine UI event handler
    /// (AGENTS.md §6) — and the body is wrapped so close always proceeds even if persistence throws.
    /// <para>
    /// Flow: the framework's synchronous <c>Closing</c> event cannot be awaited, so we
    /// <see cref="AppWindowClosingEventArgs.Cancel"/> the first close, run the async persistence
    /// (column layouts, captured placement, <see cref="MainShellViewModel.PersistStateAsync"/>), then in
    /// the <c>finally</c> close the auxiliary log windows and call <see cref="Close"/> again. The
    /// <see cref="_statePersisted"/> guard ensures that second, programmatic close is allowed straight
    /// through (and that persistence runs exactly once).
    /// </para>
    /// </remarks>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_statePersisted || _viewModel is null)
        {
            return;
        }

        // Cancel this close so we can await persistence; we re-issue Close() once done. The guard above
        // lets that second close pass through.
        args.Cancel = true;
        _statePersisted = true;

        try
        {
            ShellView.CapturePaneColumnLayouts();
            _viewModel.MainWindowPlacement = _windowManager.Capture();
            await _viewModel.PersistStateAsync();
        }
        finally
        {
            // WinUI has no child-window concept; auxiliary windows must be closed explicitly.
            MessageLogWindow.CloseAll();
            Close();
        }
    }

    /// <summary>
    /// Supplies the message-log opener delegate, but only in <c>DEBUG</c> builds.
    /// </summary>
    /// <param name="openMessageLogWindow">Set to <see cref="OpenMessageLogWindow"/> in Debug; left unchanged otherwise.</param>
    /// <remarks>
    /// <see cref="ConditionalAttribute"/> elides the call entirely in non-Debug configurations, so the
    /// in-app message log is a development-only affordance and the delegate stays <see langword="null"/>.
    /// </remarks>
    [Conditional("DEBUG")]
    private void ConfigureMessageLogWindowAction(ref Action? openMessageLogWindow)
    {
        openMessageLogWindow = OpenMessageLogWindow;
    }

    /// <summary>
    /// Opens a fresh message-log window, replacing any currently-open one.
    /// </summary>
    /// <remarks>
    /// The shared <see cref="MessageLogStore"/> is resolved from the global container (no constructor
    /// injection available here). Existing log windows are closed first so only one is ever shown.
    /// </remarks>
    private void OpenMessageLogWindow()
    {
        var store = App.Services.GetRequiredService<MessageLogStore>();
        MessageLogWindow.CloseAll();
        var window = new MessageLogWindow(store);
        window.Activate();
    }
}
