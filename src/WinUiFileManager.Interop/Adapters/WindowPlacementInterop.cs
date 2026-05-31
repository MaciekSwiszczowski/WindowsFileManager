using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Static helpers over the Win32 windowing/monitor APIs (<c>GetWindowPlacement</c>, <c>MonitorFromRect</c>,
/// <c>GetMonitorInfo</c>, <c>EnumDisplayMonitors</c>) used to capture and validate window placement when
/// persisting/restoring the app's window position across sessions. Translates native <c>RECT</c>/monitor data
/// into the small framework-agnostic value types <see cref="WindowPlacementInteropSnapshot"/> and
/// <see cref="MonitorWorkArea"/>.
/// </summary>
/// <remarks>
/// Stateless and side-effect-free (pure queries); safe to call from any thread, though placement of a live HWND
/// is most meaningful from the UI thread that owns it. All public members return a nullable value and yield
/// <see langword="null"/> rather than throwing when the OS query fails or no monitor matches.
/// </remarks>
public static class WindowPlacementInterop
{
    // SW_SHOWMAXIMIZED: WINDOWPLACEMENT.showCmd value indicating the window is currently maximized.
    private const int SwShowMaximized = 3;
    // MONITORINFOF_PRIMARY: dwFlags bit set on the primary display.
    private const uint MonitorInfoPrimary = 1;

    /// <summary>Captures the restored (non-maximized) bounds, maximized flag, and owning monitor of a window.</summary>
    /// <param name="hwnd">Native window handle.</param>
    /// <returns>A snapshot, or <see langword="null"/> if <c>GetWindowPlacement</c> fails.</returns>
    /// <remarks>
    /// Uses <c>rcNormalPosition</c> (the *restored* rectangle) deliberately so a window saved while maximized still
    /// restores to a sensible size once un-maximized; the maximized state is carried separately.
    /// </remarks>
    public static WindowPlacementInteropSnapshot? Capture(nint hwnd)
    {
        unsafe
        {
            var placement = new WINDOWPLACEMENT
            {
                length = (uint)sizeof(WINDOWPLACEMENT), // GetWindowPlacement requires length be pre-set.
            };

            var hasPlacement = PInvoke.GetWindowPlacement(new HWND(hwnd), &placement);
            if (!hasPlacement)
            {
                return null;
            }

            var bounds = placement.rcNormalPosition;
            // Clamp width/height to >=1 so a degenerate/zero rect never produces an invalid restore size.
            return new WindowPlacementInteropSnapshot(
                bounds.left,
                bounds.top,
                Math.Max(1, bounds.right - bounds.left),
                Math.Max(1, bounds.bottom - bounds.top),
                (int)placement.showCmd == SwShowMaximized,
                GetMonitorDeviceName(bounds));
        }
    }

    /// <summary>Returns the device name of the monitor nearest the given rectangle, or <see langword="null"/>.</summary>
    public static string? GetMonitorDeviceName(int x, int y, int width, int height) =>
        GetMonitorDeviceName(CreateRect(x, y, width, height));

    /// <summary>
    /// Returns the work area of a monitor that genuinely intersects the given rectangle, or <see langword="null"/>
    /// when none does. Used to detect that a persisted window position has fallen off all current displays
    /// (e.g. a monitor was unplugged) so the caller can reposition it.
    /// </summary>
    public static MonitorWorkArea? GetIntersectingMonitor(int x, int y, int width, int height)
    {
        unsafe
        {
            var bounds = CreateRect(x, y, width, height);
            // MONITOR_DEFAULTTONULL: return no monitor (rather than the nearest) when the rect is fully off-screen,
            // and additionally require a real geometric intersection below to reject "barely touching" edge cases.
            var monitor = PInvoke.MonitorFromRect(&bounds, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
            return TryGetMonitorWorkArea(monitor, out var workArea) && Intersects(bounds, ToRect(workArea))
                ? workArea
                : null;
        }
    }

    /// <summary>Returns the primary monitor's work area, or <see langword="null"/> if it cannot be found.</summary>
    public static MonitorWorkArea? GetPrimaryMonitor() => FindPrimaryMonitor();

    private static MonitorWorkArea? FindPrimaryMonitor()
    {
        MonitorWorkArea? result = null;
        unsafe
        {
            // EnumDisplayMonitors invokes the callback once per monitor. Returning true continues enumeration,
            // false stops it — so we keep going until the primary monitor is found, then short-circuit.
            PInvoke.EnumDisplayMonitors(
                default,
                null,
                (monitor, _, _, _) =>
                {
                    if (!TryGetMonitorInfo(monitor, out var monitorInfo)
                        || (monitorInfo.monitorInfo.dwFlags & MonitorInfoPrimary) == 0)
                    {
                        return true; // not the primary (or unreadable) — keep enumerating.
                    }

                    result = ToWorkArea(monitorInfo);
                    return false; // found the primary — stop.
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
                // cbSize must be the size of the EXW (extended, includes szDevice) variant so GetMonitorInfo fills
                // in the device name; setting it to sizeof(MONITORINFO) would silently drop szDevice.
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            }
        };

        unsafe
        {
            fixed (MONITORINFOEXW* monitorInfoPointer = &monitorInfo)
            {
                // Cast to MONITORINFO* is safe: MONITORINFOEXW begins with an embedded MONITORINFO; cbSize tells the
                // OS the real (larger) buffer size. Guard against a null monitor handle first.
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

    // szDevice is a fixed 32-char buffer; reinterpret as a span and trim at the null terminator to get the
    // \\.\DISPLAYn device name used to correlate a saved window with a specific monitor on the next launch.
    private static string GetDeviceName(MONITORINFOEXW monitorInfo)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<__char_32, char>(ref monitorInfo.szDevice),
            32);
        var terminatorIndex = span.IndexOf('\0');
        return new string(terminatorIndex >= 0 ? span[..terminatorIndex] : span);
    }
}
