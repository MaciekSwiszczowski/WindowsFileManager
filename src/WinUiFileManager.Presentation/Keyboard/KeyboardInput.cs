namespace WinUiFileManager.Presentation.Keyboard;

public sealed record KeyboardInput(
    VirtualKey Key,
    bool Control,
    bool Shift,
    bool Alt);
