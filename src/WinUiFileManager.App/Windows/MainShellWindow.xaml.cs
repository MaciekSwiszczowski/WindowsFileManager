namespace WinUiFileManager.App.Windows;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Infrastructure.Services;
using Application.Navigation;
using Diagnostics.FileOperations;
using Presentation.MessageLogging;
using Presentation.Services;
using Presentation.ViewModels;

public sealed partial class MainShellWindow
{
    public MainShellWindow()
    {
        InitializeComponent();

        var appWindow = AppWindow;
        appWindow.SetIcon("Assets\\app-icon.ico");
        appWindow.Closing += OnAppWindowClosing;
        _windowManager = new WindowManager(this, appWindow);
        _themeManager = new WindowThemeManager(this, appWindow);
        _themeManager.Apply(ElementTheme.Dark);

        _ = App.Services.GetRequiredService<PanelNavigationService>();
        _ = App.Services.GetRequiredService<FileOperationRequestHandler>();
        ShellView.Loaded += OnShellViewLoaded;
    }

    private bool _loaded;
    private bool _initialized;
    private MainShellViewModel? _viewModel;
    private bool _statePersisted;
    private readonly WindowManager _windowManager;
    private readonly WindowThemeManager _themeManager;

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
        ShellView.Initialize(viewModel, () => OpenMessageLogWindow());
    }

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
        _ = services.GetRequiredService<RenameService>();
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_statePersisted || _viewModel is null)
        {
            return;
        }

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
            MessageLogWindow.CloseAll();
            Close();
        }
    }

    [Conditional("DEBUG")]
    private void OpenMessageLogWindow()
    {
        var store = App.Services.GetRequiredService<MessageLogStore>();
        MessageLogWindow.CloseAll();
        var window = new MessageLogWindow(store);
        window.Activate();
    }
}
