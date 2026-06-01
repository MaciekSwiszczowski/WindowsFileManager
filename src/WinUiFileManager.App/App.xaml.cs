namespace WinUiFileManager.App;

using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Composition;
using Startup;
using Application.Abstractions;
using Application.Settings;
using Presentation.ViewModels;

/// <summary>
/// WinUI application entry point and composition-root owner: builds the Autofac-backed service
/// provider, launches the main window, schedules background startup, and acts as the process-wide
/// unhandled-exception sink. Sits in the <c>App</c> layer (see AGENTS.md §2).
/// </summary>
/// <remarks>
/// Lifetime/disposal: the <see cref="AutofacServiceProvider"/> (and its underlying container) is held
/// for the whole process lifetime and is deliberately <b>not</b> disposed on shutdown. Consequently,
/// singleton services that implement <see cref="IDisposable"/> are never released by the container
/// (see AGENTS.md §5); they are treated as process-lifetime. Anything that must run cleanup on exit has
/// to be driven explicitly (e.g. window-close persistence), not via container disposal.
/// Threading: this type lives on the UI/STA thread; <see cref="OnLaunched"/> and
/// <see cref="OnUnhandledException"/> are raised by the framework on that thread.
/// </remarks>
public sealed partial class App
{
    private readonly AutofacServiceProvider _serviceProvider;
    private readonly AppSettings _startupSettings;

    /// <summary>
    /// Initializes XAML resources and builds the service provider before any window exists.
    /// </summary>
    /// <remarks>
    /// The container is built eagerly here so that <see cref="Services"/> is usable as soon as the app
    /// instance exists. The <see cref="Microsoft.UI.Xaml.Application.UnhandledException"/> hook is wired
    /// once and never removed — this object lives for the whole process, so there is no leak concern.
    /// </remarks>
    public App()
    {
        InitializeComponent();
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        _startupSettings = LoadStartupSettings();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Resolves and shows the main window, then schedules background startup work after the first frame.
    /// </summary>
    /// <param name="args">Framework-supplied launch arguments (unused).</param>
    /// <remarks>
    /// Runs on the UI thread. Settings are loaded before the window/view model is initialized so the
    /// first shown window already has the persisted placement. The remaining startup work is delayed
    /// until the first render callback so the empty shell can paint before file enumeration/binding starts.
    /// </remarks>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = _serviceProvider.GetRequiredService<MainShellViewModel>();
        viewModel.ApplyStartupSettings(_startupSettings);

        _mainWindow = new Windows.MainShellWindow();
        _mainWindow.Initialize(viewModel);
        _mainWindow.Activate();

        StartStartupChainAfterFirstRender();
    }

    private void StartStartupChainAfterFirstRender()
    {
        CompositionTarget.Rendering += OnFirstRender;
        return;

        void OnFirstRender(object? sender, object e)
        {
            CompositionTarget.Rendering -= OnFirstRender;
            _serviceProvider.GetRequiredService<StartupChainRunner>().Start(_startupSettings);
        }
    }

    private AppSettings LoadStartupSettings()
    {
        try
        {
            return _serviceProvider
                .GetRequiredService<ISettingsRepository>()
                .Load();
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<App>();
            logger?.LogError(ex, "Loading startup settings failed; using defaults.");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Process-wide last-resort exception handler: logs the failure and marks it handled.
    /// </summary>
    /// <remarks>
    /// Setting <see cref="UnhandledExceptionEventArgs.Handled"/> to <see langword="true"/> suppresses the
    /// default fail-fast for <i>every</i> exception that reaches here, so the process keeps running even
    /// after errors it did not anticipate. This trades crash-on-bug for resilience; the only record of
    /// the failure is the log line emitted here.
    /// </remarks>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<App>();
        logger?.LogError(e.Exception, "Unhandled exception: {Message}", e.Message);
        e.Handled = true;
    }

    private Windows.MainShellWindow? _mainWindow;

    /// <summary>
    /// Global access to the application's DI container, for code-behind that cannot receive
    /// constructor-injected dependencies (e.g. windows created by the framework).
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._serviceProvider;

    /// <summary>Instance accessor for the same service provider exposed by <see cref="Services"/>.</summary>
    public IServiceProvider ServiceProvider => _serviceProvider;
}
