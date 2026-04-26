namespace WinUiFileManager.Presentation.Keyboard;

public sealed class KeyboardInput(VirtualKey key, bool control, bool shift, bool alt)
{
    public VirtualKey Key { get; } = key;

    public bool Control { get; } = control;

    public bool Shift { get; } = shift;

    public bool Alt { get; } = alt;

    public bool Handled { get; set; }
}
