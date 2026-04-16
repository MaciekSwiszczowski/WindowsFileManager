namespace WinUiFileManager.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinUiFileManager.App.Composition;

public sealed partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        this.InitializeComponent();
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        this.UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = _serviceProvider.GetRequiredService<Windows.MainShellWindow>();
        _mainWindow.Activate();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<App>();
        logger?.LogError(e.Exception, "Unhandled exception: {Message}", e.Message);
        e.Handled = true;
    }

    private Window? _mainWindow;

    public static ServiceProvider Services => ((App)Current)._serviceProvider;
}
