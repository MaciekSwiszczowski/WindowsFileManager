namespace WinUiFileManager.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WinUiFileManager.App.Composition;

public sealed partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        this.InitializeComponent();
        _serviceProvider = ServiceConfiguration.ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = _serviceProvider.GetRequiredService<Windows.MainShellWindow>();
        _mainWindow.Activate();
    }

    private Window? _mainWindow;

    public static ServiceProvider Services => ((App)Current)._serviceProvider;
}
