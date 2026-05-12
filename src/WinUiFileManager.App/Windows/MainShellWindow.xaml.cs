namespace WinUiFileManager.App.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using Infrastructure.Services;
using WinUiFileManager.Application.Navigation;
using Presentation.MessageLogging;
using Presentation.Services;
using Presentation.Messaging;
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

        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = ElementTheme.Dark;
        }

        ApplyTitleBarTheme(isDark: true);

        MessengerProperties.SetMessenger(ShellView, App.Services.GetRequiredService<IMessenger>());
        _ = App.Services.GetRequiredService<PanelNavigationService>();
        ShellView.GoToPathCommandHandler = App.Services.GetRequiredService<GoToPathCommandHandler>();
        ShellView.Loaded += OnShellViewLoaded;
    }

    private bool _loaded;
    private bool _initialized;
    private MainShellViewModel? _viewModel;
    private bool _statePersisted;
    private readonly WindowManager _windowManager;

    public void Initialize(MainShellViewModel viewModel)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _viewModel = viewModel;

        _windowManager.Initialize(viewModel);
        ShellView.ToggleThemeAction = ToggleTheme;
#if DEBUG
        ShellView.Initialize(viewModel, OpenMessageLogWindow);
#else
        ShellView.Initialize(viewModel);
#endif
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

    private void ToggleTheme()
    {
        if (Content is not FrameworkElement root)
        {
            return;
        }

        var newTheme = root.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;

        root.RequestedTheme = newTheme;
        ApplyTitleBarTheme(newTheme == ElementTheme.Dark);
    }

    private void ApplyTitleBarTheme(bool isDark)
    {
        var titleBar = AppWindow.TitleBar;

        if (isDark)
        {
            var bg = global::Windows.UI.Color.FromArgb(255, 32, 32, 32);
            var hoverBg = global::Windows.UI.Color.FromArgb(255, 51, 51, 51);
            var pressedBg = global::Windows.UI.Color.FromArgb(255, 70, 70, 70);
            var fg = Colors.White;
            var inactiveFg = global::Windows.UI.Color.FromArgb(255, 153, 153, 153);

            titleBar.BackgroundColor = bg;
            titleBar.ForegroundColor = fg;
            titleBar.InactiveBackgroundColor = bg;
            titleBar.InactiveForegroundColor = inactiveFg;
            titleBar.ButtonBackgroundColor = bg;
            titleBar.ButtonForegroundColor = fg;
            titleBar.ButtonHoverBackgroundColor = hoverBg;
            titleBar.ButtonHoverForegroundColor = fg;
            titleBar.ButtonPressedBackgroundColor = pressedBg;
            titleBar.ButtonPressedForegroundColor = fg;
            titleBar.ButtonInactiveBackgroundColor = bg;
            titleBar.ButtonInactiveForegroundColor = inactiveFg;
        }
        else
        {
            var bg = global::Windows.UI.Color.FromArgb(255, 243, 243, 243);
            var hoverBg = global::Windows.UI.Color.FromArgb(255, 229, 229, 229);
            var pressedBg = global::Windows.UI.Color.FromArgb(255, 204, 204, 204);
            var fg = Colors.Black;
            var inactiveFg = global::Windows.UI.Color.FromArgb(255, 102, 102, 102);

            titleBar.BackgroundColor = bg;
            titleBar.ForegroundColor = fg;
            titleBar.InactiveBackgroundColor = bg;
            titleBar.InactiveForegroundColor = inactiveFg;
            titleBar.ButtonBackgroundColor = bg;
            titleBar.ButtonForegroundColor = fg;
            titleBar.ButtonHoverBackgroundColor = hoverBg;
            titleBar.ButtonHoverForegroundColor = fg;
            titleBar.ButtonPressedBackgroundColor = pressedBg;
            titleBar.ButtonPressedForegroundColor = fg;
            titleBar.ButtonInactiveBackgroundColor = bg;
            titleBar.ButtonInactiveForegroundColor = inactiveFg;
        }
    }

#if DEBUG
    private void OpenMessageLogWindow()
    {
        var store = App.Services.GetRequiredService<MessageLogStore>();
        MessageLogWindow.CloseAll();
        var window = new MessageLogWindow(store);
        window.Activate();
    }
#endif
}
