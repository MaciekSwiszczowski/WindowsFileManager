using WinUiFileManager.Application.Settings;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.App.Windows;

internal static class WindowPlacementResolver
{
    public static WindowPlacement ResolveVisible(WindowPlacement placement)
    {
        if (!placement.HasRestoredPosition)
        {
            return placement;
        }

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

        var primaryMonitor = WindowPlacementInterop.GetPrimaryMonitor();
        return primaryMonitor is null
            ? placement
            : CenterOnMonitor(WindowPlacement.Default, primaryMonitor.Value);
    }

    private static WindowPlacement CenterOnMonitor(WindowPlacement placement, MonitorWorkArea work)
    {
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
