namespace WinUiFileManager.App.Windows;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

/// <summary>
/// Applies and toggles the window's element theme and keeps the custom title-bar colors in sync with it.
/// App layer; UI-thread affine. Holds the current theme so <see cref="ToggleTheme"/> can flip it.
/// </summary>
internal sealed class WindowThemeManager
{
    private static readonly TitleBarThemeColors DarkTitleBarColors = new(
        Background: global::Windows.UI.Color.FromArgb(255, 32, 32, 32),
        ButtonHoverBackground: global::Windows.UI.Color.FromArgb(255, 51, 51, 51),
        ButtonPressedBackground: global::Windows.UI.Color.FromArgb(255, 70, 70, 70),
        Foreground: Colors.White,
        InactiveForeground: global::Windows.UI.Color.FromArgb(255, 153, 153, 153));

    private static readonly TitleBarThemeColors LightTitleBarColors = new(
        Background: global::Windows.UI.Color.FromArgb(255, 243, 243, 243),
        ButtonHoverBackground: global::Windows.UI.Color.FromArgb(255, 229, 229, 229),
        ButtonPressedBackground: global::Windows.UI.Color.FromArgb(255, 204, 204, 204),
        Foreground: Colors.Black,
        InactiveForeground: global::Windows.UI.Color.FromArgb(255, 102, 102, 102));

    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private ElementTheme _currentTheme = ElementTheme.Default;

    public WindowThemeManager(Window window, AppWindow appWindow)
    {
        _window = window;
        _appWindow = appWindow;
    }

    /// <summary>
    /// Applies <paramref name="theme"/> to the window root and the title bar, and records it as current.
    /// </summary>
    /// <param name="theme">The element theme to apply. Title-bar colors track dark vs. non-dark.</param>
    /// <remarks>Must run on the UI thread (mutates framework elements and the AppWindow title bar).</remarks>
    public void Apply(ElementTheme theme)
    {
        _currentTheme = theme;

        if (_window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        ApplyTitleBarTheme(theme == ElementTheme.Dark);
    }

    /// <summary>Flips between dark and light themes based on the currently applied theme.</summary>
    public void ToggleTheme()
    {
        var nextTheme = _currentTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;

        Apply(nextTheme);
    }

    private void ApplyTitleBarTheme(bool isDark)
    {
        var colors = isDark ? DarkTitleBarColors : LightTitleBarColors;
        var titleBar = _appWindow.TitleBar;

        titleBar.BackgroundColor = colors.Background;
        titleBar.ForegroundColor = colors.Foreground;
        titleBar.InactiveBackgroundColor = colors.Background;
        titleBar.InactiveForegroundColor = colors.InactiveForeground;
        titleBar.ButtonBackgroundColor = colors.Background;
        titleBar.ButtonForegroundColor = colors.Foreground;
        titleBar.ButtonHoverBackgroundColor = colors.ButtonHoverBackground;
        titleBar.ButtonHoverForegroundColor = colors.Foreground;
        titleBar.ButtonPressedBackgroundColor = colors.ButtonPressedBackground;
        titleBar.ButtonPressedForegroundColor = colors.Foreground;
        titleBar.ButtonInactiveBackgroundColor = colors.Background;
        titleBar.ButtonInactiveForegroundColor = colors.InactiveForeground;
    }

    /// <summary>Immutable palette for the custom title bar in one theme (dark or light).</summary>
    private sealed record TitleBarThemeColors(
        global::Windows.UI.Color Background,
        global::Windows.UI.Color ButtonHoverBackground,
        global::Windows.UI.Color ButtonPressedBackground,
        global::Windows.UI.Color Foreground,
        global::Windows.UI.Color InactiveForeground);
}
