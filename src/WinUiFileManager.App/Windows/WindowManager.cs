using Windows.Graphics;

namespace WinUiFileManager.App.Windows;

using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Application.Settings;
using Interop.Adapters;
using Presentation.ViewModels;
using WinRT.Interop;

/// <summary>
/// Two-way bridge between the main window's physical placement (position/size/maximized state) and the
/// <see cref="MainShellViewModel.MainWindowPlacement"/> property: applies persisted placement on startup
/// and writes live changes back to the view model. App layer; UI-thread affine.
/// </summary>
/// <remarks>
/// <b>Leak hazard (documented, not fixed — see AGENTS.md §5).</b> <see cref="Initialize"/> subscribes to
/// <see cref="MainShellViewModel.PropertyChanged"/>, the window's <c>SizeChanged</c>, and the
/// <see cref="AppWindow"/>'s <c>Changed</c> event, but there is no matching unsubscribe anywhere. The
/// view model is a DI singleton that outlives any single window, so the <c>PropertyChanged += </c>
/// subscription roots this <see cref="WindowManager"/> (and transitively the window) for the lifetime of
/// the view model. This is the cautionary example called out in AGENTS.md; do not "fix" it as a side
/// effect — it is described here so future edits are aware of it.
/// <para>
/// Re-entrancy: <see cref="_trackingEnabled"/> and <see cref="_updatingViewModelPlacement"/> are guards
/// that prevent the apply→capture→apply feedback loop between the window and the view model.
/// </para>
/// </remarks>
internal sealed class WindowManager
{
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private MainShellViewModel? _viewModel;
    private bool _trackingEnabled;
    private bool _updatingViewModelPlacement;

    public WindowManager(Window window, AppWindow appWindow)
    {
        _window = window;
        _appWindow = appWindow;
    }

    /// <summary>
    /// Applies the view model's saved placement to the window and begins tracking live changes.
    /// </summary>
    /// <param name="viewModel">The shell view model that owns the persisted placement. Outlives the window.</param>
    /// <remarks>
    /// Must run on the UI thread (mutates window state). The event subscriptions made here are never
    /// removed — see the leak-hazard note on the type. Tracking is enabled only after the initial
    /// <see cref="Apply"/> so that programmatic placement does not get echoed straight back as a capture.
    /// </remarks>
    public void Initialize(MainShellViewModel viewModel)
    {
        _viewModel = viewModel;
        Apply(viewModel.MainWindowPlacement);
        _trackingEnabled = true;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _window.SizeChanged += OnWindowSizeChanged;
        _appWindow.Changed += OnAppWindowChanged;
    }

    /// <summary>
    /// Reads the current window placement into a persistable <see cref="WindowPlacement"/>.
    /// </summary>
    /// <returns>The current position/size/maximized state and the device name of the hosting monitor.</returns>
    /// <remarks>
    /// UI-thread affine. When the window is maximized/minimized (not <c>Restored</c>), the AppWindow
    /// reports the current bounds rather than the restore bounds, so we go to the native Win32
    /// <c>WINDOWPLACEMENT</c> (which carries the restore rectangle) to preserve the un-maximized size for
    /// next launch. Only when that native read is unavailable do we fall back to AppWindow geometry.
    /// </remarks>
    public WindowPlacement Capture()
    {
        if (_appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            // Maximized/minimized: AppWindow bounds aren't the restore bounds, so query native
            // WINDOWPLACEMENT to recover the restored rectangle.
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

    /// <summary>
    /// Moves/resizes/maximizes the window to match <paramref name="placement"/>, after clamping it to a
    /// currently-visible monitor.
    /// </summary>
    /// <param name="placement">The desired placement (typically from persisted view-model state).</param>
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Ignore unrelated property changes and re-entrant notifications raised by our own Capture write.
        if (args.PropertyName != nameof(MainShellViewModel.MainWindowPlacement)
            || _viewModel is null
            || _updatingViewModelPlacement)
        {
            return;
        }

        // Suppress tracking while applying so the resulting window events don't immediately re-capture
        // and overwrite the value we are applying.
        var trackingEnabled = _trackingEnabled;
        _trackingEnabled = false;
        try
        {
            Apply(_viewModel.MainWindowPlacement);
        }
        finally
        {
            _trackingEnabled = trackingEnabled;
        }
    }

    /// <summary>
    /// Captures the live window placement back into the view model, unless tracking is suppressed or the
    /// window is not in the restored state.
    /// </summary>
    /// <remarks>
    /// We only persist while restored: capturing geometry mid-maximize/minimize would store transient
    /// bounds. The <see cref="_updatingViewModelPlacement"/> flag marks the write as self-originated so
    /// <see cref="OnViewModelPropertyChanged"/> ignores the resulting notification.
    /// </remarks>
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

        _updatingViewModelPlacement = true;
        try
        {
            _viewModel.MainWindowPlacement = Capture();
        }
        finally
        {
            _updatingViewModelPlacement = false;
        }
    }
}
