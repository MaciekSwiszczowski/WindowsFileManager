using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Settings;

[StructLayout(LayoutKind.Auto)]
public readonly record struct WindowPlacement(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsMaximized,
    string? DisplayDeviceName = null)
{
    public static WindowPlacement Default { get; } = new(
        X: int.MinValue,
        Y: int.MinValue,
        Width: 1400,
        Height: 900,
        IsMaximized: false,
        DisplayDeviceName: null);

    public bool HasRestoredPosition => X != int.MinValue && Y != int.MinValue;
}
