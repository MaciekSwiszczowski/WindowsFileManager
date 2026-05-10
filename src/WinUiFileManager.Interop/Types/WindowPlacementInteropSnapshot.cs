namespace WinUiFileManager.Interop.Types;

public readonly record struct WindowPlacementInteropSnapshot(int X, int Y, int Width, int Height, bool IsMaximized, string? DisplayDeviceName);
