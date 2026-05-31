namespace WinUiFileManager.Presentation.Keyboard;

/// <summary>
/// A snapshot of a single key gesture (the pressed <see cref="Key"/> plus the Ctrl/Shift/Alt modifier
/// state) passed as the command parameter from <see cref="KeyboardInputBehavior"/> to
/// <see cref="KeyboardManager"/>. <see cref="Handled"/> is the out-channel: the manager sets it to mark
/// the gesture consumed, which the behavior copies back onto the routed event.
/// </summary>
public sealed class KeyboardInput(VirtualKey key, bool control, bool shift, bool alt)
{
    /// <summary>The non-modifier key that was pressed.</summary>
    public VirtualKey Key { get; } = key;

    /// <summary>Whether Ctrl was held.</summary>
    public bool Control { get; } = control;

    /// <summary>Whether Shift was held.</summary>
    public bool Shift { get; } = shift;

    /// <summary>Whether Alt (Menu) was held.</summary>
    public bool Alt { get; } = alt;

    /// <summary>Set by the handler to true when the gesture was acted on; the behavior then marks the
    /// underlying <c>PreviewKeyDown</c> as handled so it does not bubble further.</summary>
    public bool Handled { get; set; }
}
