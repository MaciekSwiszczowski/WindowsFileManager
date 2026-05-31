namespace WinUiFileManager.Interop.Types;

/// <summary>
/// A monitor's usable work area (screen rectangle excluding the taskbar) plus its device name. Returned by
/// <see cref="Adapters.WindowPlacementInterop"/> and used to validate/clamp persisted window placement to a
/// currently-connected display.
/// </summary>
/// <param name="X">Left edge of the work area, in virtual-screen pixels.</param>
/// <param name="Y">Top edge of the work area, in virtual-screen pixels.</param>
/// <param name="Width">Work-area width (always &gt;= 1).</param>
/// <param name="Height">Work-area height (always &gt;= 1).</param>
/// <param name="DeviceName">The <c>\\.\DISPLAYn</c> device name identifying the monitor.</param>
public readonly record struct MonitorWorkArea(int X, int Y, int Width, int Height, string DeviceName);
