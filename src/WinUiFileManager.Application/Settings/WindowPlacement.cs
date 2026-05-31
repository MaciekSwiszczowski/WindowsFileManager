using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Persisted main-window placement (position, size, maximized state, and the monitor it was on), part of
/// <see cref="AppSettings"/>. A value struct stored inline.
/// </summary>
/// <param name="X">Left position in screen pixels; <see cref="int.MinValue"/> means "no saved position".</param>
/// <param name="Y">Top position in screen pixels; <see cref="int.MinValue"/> means "no saved position".</param>
/// <param name="Width">Window width in pixels.</param>
/// <param name="Height">Window height in pixels.</param>
/// <param name="IsMaximized">Whether the window was maximized.</param>
/// <param name="DisplayDeviceName">Name of the monitor the window was on, to restore onto the same display when present.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WindowPlacement(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsMaximized,
    string? DisplayDeviceName = null)
{
    /// <summary>First-run default: centered-by-OS (sentinel position), 1400x900, not maximized.</summary>
    public static WindowPlacement Default { get; } = new(
        X: int.MinValue,
        Y: int.MinValue,
        Width: 1400,
        Height: 900,
        IsMaximized: false,
        DisplayDeviceName: null);

    /// <summary>Whether a real saved position exists (i.e. <see cref="X"/>/<see cref="Y"/> are not the sentinel).</summary>
    public bool HasRestoredPosition => X != int.MinValue && Y != int.MinValue;
}
