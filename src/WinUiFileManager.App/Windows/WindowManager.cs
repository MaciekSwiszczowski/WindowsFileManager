using Windows.Graphics;

namespace WinUiFileManager.App.Windows;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Application.Settings;
using Interop.Adapters;
using Presentation.ViewModels;
using WinRT.Interop;

internal sealed class WindowManager
{
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private MainShellViewModel? _viewModel;
    private bool _trackingEnabled;

    public WindowManager(Window window, AppWindow appWindow)
    {
        _window = window;
        _appWindow = appWindow;
    }

    public void Initialize(MainShellViewModel viewModel)
    {
        _viewModel = viewModel;
        Apply(viewModel.MainWindowPlacement);
        _trackingEnabled = true;
        _window.SizeChanged += OnWindowSizeChanged;
        _appWindow.Changed += OnAppWindowChanged;
    }

    public WindowPlacement Capture()
    {
        if (_appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            var nativePlacement = WindowPlacementInterop.Capture(WindowNative.GetWindowHandle(_window));
            if (nativePlacement is not null)
            {
                return ToWindowPlacement(nativePlacement.Value);
            }
        }

        var monitorDeviceName = WindowPlacementInterop
            .GetMonitorDeviceName(_appWindow.Position.X, _appWindow.Position.Y, _appWindow.Size.Width, _appWindow.Size.Height);

        return new WindowPlacement(X: _appWindow.Position.X, Y: _appWindow.Position.Y,
            Width: _appWindow.Size.Width, Height: _appWindow.Size.Height,
            IsMaximized: false,
            DisplayDeviceName: monitorDeviceName);
    }

    private void Apply(WindowPlacement placement)
    {
        var resolved = WindowPlacementResolver.ResolveVisible(placement);

        if (resolved.HasRestoredPosition)
        {
            _appWindow.Move(new PointInt32(resolved.X, resolved.Y));
        }

        var size = new SizeInt32(resolved.Width, resolved.Height);
        _appWindow.Resize(size);

        if (resolved.IsMaximized && _appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private static WindowPlacement ToWindowPlacement(Interop.Types.WindowPlacementInteropSnapshot snapshot) =>
        new(
            X: snapshot.X,
            Y: snapshot.Y,
            Width: snapshot.Width,
            Height: snapshot.Height,
            IsMaximized: snapshot.IsMaximized,
            DisplayDeviceName: snapshot.DisplayDeviceName);

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
        => UpdateRestoredPlacement();

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args is { DidPositionChange: false, DidSizeChange: false, DidPresenterChange: false })
        {
            return;
        }

        UpdateRestoredPlacement();
    }

    private void UpdateRestoredPlacement()
    {
        if (!_trackingEnabled || _viewModel is null)
        {
            return;
        }

        if (_appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            return;
        }

        _viewModel.MainWindowPlacement = Capture();
    }
}
