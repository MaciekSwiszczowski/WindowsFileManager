namespace WinUiFileManager.App.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
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
        appWindow.Closing += OnAppWindowClosing;

        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = ElementTheme.Dark;
        }

        ApplyTitleBarTheme(isDark: true);

        ShellView.Loaded += OnShellViewLoaded;
    }

    private bool _initialized;
    private MainShellViewModel? _viewModel;
    private bool _statePersisted;

    private async void OnShellViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        var services = App.Services;

        _viewModel = services.GetRequiredService<MainShellViewModel>();
        _viewModel.LeftPane.PaneId = PaneId.Left;
        _viewModel.RightPane.PaneId = PaneId.Right;

        var dialogService = services.GetRequiredService<WinUiDialogService>();
        dialogService.XamlRoot = ShellView.XamlRoot;

        ShellView.Initialize(_viewModel);
        ShellView.ToggleThemeAction = ToggleTheme;

        if (_viewModel is not null)
        {
            await _viewModel.InitializeAsync();
            ApplyPlacement(_viewModel.MainWindowPlacement);
        }
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
            _viewModel.MainWindowPlacement = CaptureCurrentPlacement();
            await _viewModel.PersistStateAsync();
        }
        finally
        {
            this.Close();
        }
    }

    private void ApplyPlacement(WindowPlacement placement)
    {
        var clamped = ClampToPrimaryDisplay(placement);

        if (clamped.HasRestoredPosition)
        {
            AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(
                clamped.X,
                clamped.Y,
                clamped.Width,
                clamped.Height));
        }
        else
        {
            AppWindow.Resize(new global::Windows.Graphics.SizeInt32(clamped.Width, clamped.Height));
        }

        if (clamped.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private WindowPlacement CaptureCurrentPlacement()
    {
        var position = AppWindow.Position;
        var size = AppWindow.Size;
        var isMaximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };

        return new WindowPlacement(
            X: position.X,
            Y: position.Y,
            Width: size.Width,
            Height: size.Height,
            IsMaximized: isMaximized);
    }

    private static WindowPlacement ClampToPrimaryDisplay(WindowPlacement placement)
    {
        if (!placement.HasRestoredPosition)
        {
            return placement;
        }

        var probePoint = new global::Windows.Graphics.PointInt32(placement.X, placement.Y);
        var display = DisplayArea.GetFromPoint(probePoint, DisplayAreaFallback.None);
        if (display is null)
        {
            var primary = DisplayArea.Primary;
            var work = primary.WorkArea;
            var centeredX = work.X + Math.Max(0, (work.Width - placement.Width) / 2);
            var centeredY = work.Y + Math.Max(0, (work.Height - placement.Height) / 2);
            return placement with { X = centeredX, Y = centeredY };
        }

        return placement;
    }

    public void ToggleTheme()
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
}
