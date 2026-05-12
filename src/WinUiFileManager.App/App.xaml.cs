namespace WinUiFileManager.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Composition;
using WinUiFileManager.Presentation.ViewModels;

public sealed partial class App
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        InitializeComponent();
        _serviceProvider = ServiceConfiguration.ConfigureServices();
        UnhandledException += OnUnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = _serviceProvider.GetRequiredService<MainShellViewModel>();
        await viewModel.InitializeAsync();

        var mainWindow = _serviceProvider.GetRequiredService<Windows.MainShellWindow>();
        mainWindow.Initialize(viewModel);

        _mainWindow = mainWindow;
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
    public ServiceProvider ServiceProvider => _serviceProvider;
}
