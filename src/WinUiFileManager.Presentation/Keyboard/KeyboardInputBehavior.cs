using System.Windows.Input;
using Windows.UI.Core;

namespace WinUiFileManager.Presentation.Keyboard;

public static class KeyboardInputBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(KeyboardInputBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(UIElement element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(UIElement element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if (e.OldValue is not null)
        {
            element.PreviewKeyDown -= Element_PreviewKeyDown;
        }

        if (e.NewValue is not null)
        {
            element.PreviewKeyDown += Element_PreviewKeyDown;
        }
    }

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

        if (!command.CanExecute(input))
        {
            return;
        }

        command.Execute(input);
        e.Handled = true;
    }

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
