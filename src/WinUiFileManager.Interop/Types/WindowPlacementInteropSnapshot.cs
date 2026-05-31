namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Snapshot of a window's persisted placement: its restored (non-maximized) bounds, whether it is currently
/// maximized, and the monitor it lives on. Produced by <see cref="Adapters.WindowPlacementInterop.Capture"/> and
/// serialized to settings so the window can be restored across sessions.
/// </summary>
/// <param name="X">Left edge of the restored window rectangle.</param>
/// <param name="Y">Top edge of the restored window rectangle.</param>
/// <param name="Width">Restored window width (always &gt;= 1).</param>
/// <param name="Height">Restored window height (always &gt;= 1).</param>
/// <param name="IsMaximized"><see langword="true"/> if the window was maximized when captured.</param>
/// <param name="DisplayDeviceName">The <c>\\.\DISPLAYn</c> device name of the owning monitor, or <see langword="null"/> if unknown.</param>
public readonly record struct WindowPlacementInteropSnapshot(int X, int Y, int Width, int Height, bool IsMaximized, string? DisplayDeviceName);
