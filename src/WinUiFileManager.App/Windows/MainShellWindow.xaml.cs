namespace WinUiFileManager.App.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;

public sealed partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        this.InitializeComponent();

        var appWindow = this.AppWindow;
        appWindow.Resize(new global::Windows.Graphics.SizeInt32(1400, 900));
        appWindow.SetIcon("Assets\\app-icon.ico");

        if (Content is FrameworkElement root)
            root.RequestedTheme = ElementTheme.Dark;

        ApplyTitleBarTheme(isDark: true);

        ShellView.Loaded += OnShellViewLoaded;
    }

    private bool _initialized;

    private async void OnShellViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;
        _initialized = true;

        var services = App.Services;

        var viewModel = services.GetRequiredService<MainShellViewModel>();
        viewModel.LeftPane.PaneId = PaneId.Left;
        viewModel.RightPane.PaneId = PaneId.Right;

        var dialogService = services.GetRequiredService<WinUiDialogService>();
        dialogService.XamlRoot = ShellView.XamlRoot;

        ShellView.Initialize(viewModel);
        ShellView.ToggleThemeAction = ToggleTheme;

        await Task.Yield();
        await viewModel.InitializeAsync();
    }

    public void ToggleTheme()
    {
        if (Content is not FrameworkElement root)
            return;

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
}
