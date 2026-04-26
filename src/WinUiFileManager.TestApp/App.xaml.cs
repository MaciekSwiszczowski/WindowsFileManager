namespace WinUiFileManager.TestApp;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        CreateMainWindow();
    }

    private void CreateMainWindow()
    {
        try
        {
            _mainWindow = new MainWindow();
            _mainWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            throw;
        }
    }
}
