using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.Keyboard;

public sealed class KeyboardManager
{
    private readonly IMessenger _messenger;

    public KeyboardManager(IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        KeyPressedCommand = new KeyboardInputCommand(this);
    }

    public ICommand KeyPressedCommand { get; }

    private void Handle(KeyboardInput input)
    {
        switch (input)
        {
            case { Key: VirtualKey.Up, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionUpMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Down, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionDownMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.PageUp, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionPageUpMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.PageDown, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionPageDownMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Home, Shift: true, Alt: false }:
                Send(new ExtendSelectionHomeMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.End, Shift: true, Alt: false }:
                Send(new ExtendSelectionEndMessage());
                input.Handled = true;
                break;

            case { Key: VirtualKey.Up, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorUpMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Down, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorDownMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.PageUp, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorPageUpMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.PageDown, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorPageDownMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Home, Shift: false, Alt: false }:
                Send(new MoveCursorHomeMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.End, Shift: false, Alt: false }:
                Send(new MoveCursorEndMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Space, Shift: false, Alt: false }:
                Send(new ToggleSelectionAtCursorMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Insert, Control: false, Shift: false, Alt: false }:
                Send(new ToggleSelectionAtCursorAndAdvanceMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.A, Control: true, Shift: false, Alt: false }:
                Send(new SelectAllMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.A, Control: true, Shift: true, Alt: false }:
                Send(new ClearSelectionMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Escape, Control: false, Shift: false, Alt: false }:
                Send(new ClearSelectionMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: false }:
                Send(new ActivateInvokedMessage());
                input.Handled = true;
                break;

            case { Key: VirtualKey.Back, Control: false, Shift: false, Alt: false }:
                Send(new NavigateUpKeyPressedMessage());
                input.Handled = true;
                break;
            case { Key: VirtualKey.PageUp, Control: true, Shift: false, Alt: false }:
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

    private sealed class KeyboardInputCommand(KeyboardManager owner) : ICommand
    {
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
