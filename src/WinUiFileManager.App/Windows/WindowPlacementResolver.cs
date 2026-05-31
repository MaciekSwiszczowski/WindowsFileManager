using WinUiFileManager.Application.Settings;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.App.Windows;

/// <summary>
/// Pure helper that clamps a persisted <see cref="WindowPlacement"/> to a monitor that currently exists,
/// so a window restored on a now-disconnected display does not open off-screen. App layer; no UI state.
/// </summary>
internal static class WindowPlacementResolver
{
    /// <summary>
    /// Returns a placement guaranteed to land on a visible monitor.
    /// </summary>
    /// <param name="placement">The persisted placement to validate.</param>
    /// <returns>
    /// The original placement when it still intersects its saved monitor; otherwise a default-sized
    /// window centered on the primary monitor. The input is returned unchanged when no monitor info is
    /// available (best-effort).
    /// </returns>
    public static WindowPlacement ResolveVisible(WindowPlacement placement)
    {
        // No saved position (first run) means there is nothing to validate.
        if (!placement.HasRestoredPosition)
        {
            return placement;
        }

        // Accept the saved placement only if it intersects a monitor AND that monitor is the same device
        // it was saved on (or we don't know the device) — guards against a different monitor occupying
        // the same coordinates.
        var monitor = WindowPlacementInterop.GetIntersectingMonitor(
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height);
        if (monitor is not null
            && (string.IsNullOrEmpty(placement.DisplayDeviceName)
                || string.Equals(placement.DisplayDeviceName, monitor.Value.DeviceName, StringComparison.OrdinalIgnoreCase)))
        {
            return placement;
        }

        // Saved monitor is gone: fall back to a default window centered on the primary display.
        var primaryMonitor = WindowPlacementInterop.GetPrimaryMonitor();
        return primaryMonitor is null
            ? placement
            : CenterOnMonitor(WindowPlacement.Default, primaryMonitor.Value);
    }

    private static WindowPlacement CenterOnMonitor(WindowPlacement placement, MonitorWorkArea work)
    {
        // Guard against zero/negative work-area dimensions so the centering math can't divide oddly.
        var workWidth = Math.Max(1, work.Width);
        var workHeight = Math.Max(1, work.Height);
        var width = placement.Width;
        var height = placement.Height;

        return placement with
        {
            X = work.X + Math.Max(0, (workWidth - width) / 2),
            Y = work.Y + Math.Max(0, (workHeight - height) / 2),
            DisplayDeviceName = work.DeviceName,
        };
    }
}
