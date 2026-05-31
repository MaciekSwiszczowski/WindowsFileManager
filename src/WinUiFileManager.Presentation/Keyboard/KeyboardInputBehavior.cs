using System.Windows.Input;
using Windows.UI.Core;

namespace WinUiFileManager.Presentation.Keyboard;

/// <summary>
/// Attached behavior that bridges an element's <c>PreviewKeyDown</c> to an <see cref="ICommand"/>. Set
/// <c>Keyboard:KeyboardInputBehavior.Command="{x:Bind ...}"</c> on a focusable element (the shell binds
/// it to <see cref="KeyboardManager.KeyPressedCommand"/>) and every key press is turned into a
/// <see cref="KeyboardInput"/> and offered to the command.
/// </summary>
/// <remarks>
/// This is the entry point of the keyboard routing model: element key press → attached behavior →
/// command → <see cref="KeyboardManager.Handle"/> → app message. The behavior owns the
/// subscribe/unsubscribe lifecycle in <see cref="OnCommandChanged"/>: it removes the
/// <c>PreviewKeyDown</c> handler when the command is cleared/replaced and adds it when set, so toggling
/// the attached command never leaks a duplicate subscription (AGENTS.md §5).
/// </remarks>
public static class KeyboardInputBehavior
{
    /// <summary>Attached DP holding the command to invoke on key press.</summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(KeyboardInputBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    /// <summary>Gets the attached <see cref="CommandProperty"/> value.</summary>
    public static ICommand? GetCommand(UIElement element) =>
        (ICommand?)element.GetValue(CommandProperty);

    /// <summary>Sets the attached <see cref="CommandProperty"/> value.</summary>
    public static void SetCommand(UIElement element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        // Detach when the command is cleared/replaced and attach when set, so the PreviewKeyDown
        // subscription stays balanced across command changes.
        if (e.OldValue is not null)
        {
            element.PreviewKeyDown -= Element_PreviewKeyDown;
        }

        if (e.NewValue is not null)
        {
            element.PreviewKeyDown += Element_PreviewKeyDown;
        }
    }

    /// <summary>Captures the current modifier state, builds a <see cref="KeyboardInput"/>, and routes it
    /// to the attached command (when it reports it can execute), copying back the command's
    /// <see cref="KeyboardInput.Handled"/> result onto the routed event.</summary>
    private static void Element_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var command = GetCommand(element);
        if (command is null)
        {
            return;
        }

        var input = new KeyboardInput(
            e.Key,
            IsKeyDown(VirtualKey.Control),
            IsKeyDown(VirtualKey.Shift),
            IsKeyDown(VirtualKey.Menu));

        if (command.CanExecute(input))
        {
            command.Execute(input);
            e.Handled = input.Handled;
        }
    }

    /// <summary>True when <paramref name="key"/> (used here for modifier keys) is currently down on the
    /// calling (UI) thread.</summary>
    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
