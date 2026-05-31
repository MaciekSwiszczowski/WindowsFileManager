using System.Windows.Input;
using WinUiFileManager.Application.Messages.RequestMessages;

namespace WinUiFileManager.Presentation.Keyboard;

/// <summary>
/// The central application keyboard map: translates a <see cref="KeyboardInput"/> gesture into the
/// corresponding app-level request message (activate, navigate up, rename, delete, copy, move, create
/// folder, copy path, toggle inspector, properties) and publishes it via the messenger.
/// </summary>
/// <remarks>
/// This is the second half of the keyboard routing model (see <see cref="KeyboardInputBehavior"/>): the
/// attached behavior delivers the gesture through <see cref="KeyPressedCommand"/>, and the
/// <see cref="Handle"/> switch decides which message to send. Each handled gesture sets
/// <see cref="KeyboardInput.Handled"/> so the behavior stops the key from bubbling. The manager only
/// sends messages — the actual operations are performed by whichever recipients are registered for those
/// messages — so it has no per-pane state and is messenger-scoped at the app level.
/// </remarks>
public sealed class KeyboardManager
{
    private readonly IMessenger _messenger;

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messenger"/> is null.</exception>
    public KeyboardManager(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _messenger = messenger;
        KeyPressedCommand = new KeyboardInputCommand(this);
    }

    /// <summary>The command exposed to XAML (via <see cref="KeyboardInputBehavior"/>) that feeds key
    /// gestures into <see cref="Handle"/>.</summary>
    public ICommand KeyPressedCommand { get; }

    /// <summary>Maps a key gesture to an app message. Modifier combinations are matched exactly via
    /// patterns so, e.g., plain F6 and Shift+F6 dispatch to different actions. Unmapped gestures are
    /// ignored (left unhandled so the event continues to bubble).</summary>
    private void Handle(KeyboardInput input)
    {
        switch (input)
        {
            case { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: false }:
                Send(new ActivateInvokedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Back, Control: false, Shift: false, Alt: false }:
                Send(new NavigateUpKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Up, Control: false, Shift: false, Alt: true }:
                Send(new NavigateUpKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F2, Control: false, Shift: false, Alt: false }:
                Send(new RenameKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F6, Control: false, Shift: true, Alt: false }:
                Send(new RenameKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Delete, Control: false, Alt: false }:
                Send(new DeleteKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F8, Control: false, Shift: false, Alt: false }:
                Send(new DeleteKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F5, Control: false, Shift: false, Alt: false }:
                Send(new CopyKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F6, Control: false, Shift: false, Alt: false }:
                Send(new MoveKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.F7, Control: false, Shift: false, Alt: false }:
                Send(new CreateFolderKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.N, Control: true, Shift: true, Alt: false }:
                Send(new CreateFolderKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.C, Control: true, Shift: true, Alt: false }:
                Send(new CopyPathKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.I, Control: true, Shift: false, Alt: false }:
                Send(new ToggleInspectorKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: true }:
                Send(new PropertiesKeyPressedMessage());
                input.Handled = true;
                break;
        }
    }

    private void Send<TMessage>(TMessage message)
        where TMessage : class
    {
        _messenger.Send(message);
    }

    /// <summary>
    /// Thin <see cref="ICommand"/> adapter exposing <see cref="KeyboardManager.Handle"/> to XAML. It only
    /// accepts a <see cref="KeyboardInput"/> parameter and never raises
    /// <see cref="ICommand.CanExecuteChanged"/> (executability does not change over time).
    /// </summary>
    private sealed class KeyboardInputCommand(KeyboardManager owner) : ICommand
    {
        // No-op accessors: this command's CanExecute result never changes, so there is nothing to raise.
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            parameter is KeyboardInput;

        public void Execute(object? parameter)
        {
            if (parameter is KeyboardInput input)
            {
                owner.Handle(input);
            }
        }
    }
}
