using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public static class WindowPlacementInterop
{
    private const int SwShowMaximized = 3;
    private const uint MonitorInfoPrimary = 1;

    public static WindowPlacementInteropSnapshot? Capture(nint hwnd)
    {
        unsafe
        {
            var placement = new WINDOWPLACEMENT
            {
                length = (uint)sizeof(WINDOWPLACEMENT),
            };

            var hasPlacement = PInvoke.GetWindowPlacement(new HWND(hwnd), &placement);
            if (!hasPlacement)
            {
                return null;
            }

            var bounds = placement.rcNormalPosition;
            return new WindowPlacementInteropSnapshot(
                bounds.left,
                bounds.top,
                Math.Max(1, bounds.right - bounds.left),
                Math.Max(1, bounds.bottom - bounds.top),
                (int)placement.showCmd == SwShowMaximized,
                GetMonitorDeviceName(bounds));
        }
    }

    public static string? GetMonitorDeviceName(int x, int y, int width, int height) =>
        GetMonitorDeviceName(CreateRect(x, y, width, height));

    public static MonitorWorkArea? GetIntersectingMonitor(int x, int y, int width, int height)
    {
        unsafe
        {
            var bounds = CreateRect(x, y, width, height);
            var monitor = PInvoke.MonitorFromRect(&bounds, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
            return TryGetMonitorWorkArea(monitor, out var workArea) && Intersects(bounds, ToRect(workArea))
                ? workArea
                : null;
        }
    }

    public static MonitorWorkArea? GetPrimaryMonitor() => FindPrimaryMonitor();

    private static MonitorWorkArea? FindPrimaryMonitor()
    {
        MonitorWorkArea? result = null;
        unsafe
        {
            PInvoke.EnumDisplayMonitors(
                default,
                null,
                (monitor, _, _, _) =>
                {
                    if (!TryGetMonitorInfo(monitor, out var monitorInfo)
                        || (monitorInfo.monitorInfo.dwFlags & MonitorInfoPrimary) == 0)
                    {
                        return true;
                    }

                    result = ToWorkArea(monitorInfo);
                    return false;
                },
                default);
        }

        return result;
    }

    private static string? GetMonitorDeviceName(RECT bounds)
    {
        unsafe
        {
            var monitor = PInvoke.MonitorFromRect(&bounds, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            return TryGetMonitorInfo(monitor, out var monitorInfo)
                ? GetDeviceName(monitorInfo)
                : null;
        }
    }

    private static bool TryGetMonitorWorkArea(HMONITOR monitor, out MonitorWorkArea workArea)
    {
        if (TryGetMonitorInfo(monitor, out var monitorInfo))
        {
            workArea = ToWorkArea(monitorInfo);
            return true;
        }

        workArea = default;
        return false;
    }

    private static bool TryGetMonitorInfo(HMONITOR monitor, out MONITORINFOEXW monitorInfo)
    {
        monitorInfo = new MONITORINFOEXW
        {
            monitorInfo = new MONITORINFO
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            }
        };

        unsafe
        {
            fixed (MONITORINFOEXW* monitorInfoPointer = &monitorInfo)
            {
                return !monitor.IsNull && PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)monitorInfoPointer);
            }
        }
    }

    private static MonitorWorkArea ToWorkArea(MONITORINFOEXW monitorInfo)
    {
        var work = monitorInfo.monitorInfo.rcWork;
        return new MonitorWorkArea(
            work.left,
            work.top,
            Math.Max(1, work.right - work.left),
            Math.Max(1, work.bottom - work.top),
            GetDeviceName(monitorInfo));
    }

    private static RECT ToRect(MonitorWorkArea workArea) =>
        new()
        {
            left = workArea.X,
            top = workArea.Y,
            right = workArea.X + workArea.Width,
            bottom = workArea.Y + workArea.Height,
        };

    private static RECT CreateRect(int x, int y, int width, int height) =>
        new()
        {
            left = x,
            top = y,
            right = x + Math.Max(1, width),
            bottom = y + Math.Max(1, height)
        };

    private static bool Intersects(RECT bounds, RECT workArea) =>
        bounds.left < workArea.right
        && bounds.right > workArea.left
        && bounds.top < workArea.bottom
        && bounds.bottom > workArea.top;

    private static string GetDeviceName(MONITORINFOEXW monitorInfo)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<__char_32, char>(ref monitorInfo.szDevice),
            32);
        var terminatorIndex = span.IndexOf('\0');
        return new string(terminatorIndex >= 0 ? span[..terminatorIndex] : span);
    }
}
