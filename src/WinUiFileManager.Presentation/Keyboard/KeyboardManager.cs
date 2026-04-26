using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using Windows.System;
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

    private bool CanHandle(KeyboardInput input) =>
        input switch
        {
            { Key: VirtualKey.Up, Shift: true, Control: false, Alt: false } => true,
            { Key: VirtualKey.Down, Shift: true, Control: false, Alt: false } => true,
            { Key: VirtualKey.PageUp, Shift: true, Control: false, Alt: false } => true,
            { Key: VirtualKey.PageDown, Shift: true, Control: false, Alt: false } => true,
            { Key: VirtualKey.Home, Shift: true, Alt: false } => true,
            { Key: VirtualKey.End, Shift: true, Alt: false } => true,

            { Key: VirtualKey.Up, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Down, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.PageUp, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.PageDown, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Home, Shift: false, Alt: false } => true,
            { Key: VirtualKey.End, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Space, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Insert, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.A, Control: true, Shift: false, Alt: false } => true,
            { Key: VirtualKey.A, Control: true, Shift: true, Alt: false } => true,
            { Key: VirtualKey.Escape, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: false } => true,

            { Key: VirtualKey.Back, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.PageUp, Control: true, Shift: false, Alt: false } => true,
            { Key: VirtualKey.Up, Control: false, Shift: false, Alt: true } => true,
            { Key: VirtualKey.F2, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.F6, Control: false, Shift: true, Alt: false } => true,
            { Key: VirtualKey.Delete, Control: false, Alt: false } => true,
            { Key: VirtualKey.F8, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.F5, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.F6, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.F7, Control: false, Shift: false, Alt: false } => true,
            { Key: VirtualKey.N, Control: true, Shift: true, Alt: false } => true,
            { Key: VirtualKey.C, Control: true, Shift: true, Alt: false } => true,
            { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: true } => true,
            _ => false,
        };

    private void Handle(KeyboardInput input)
    {
        switch (input)
        {
            case { Key: VirtualKey.Up, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionUpMessage());
                break;
            case { Key: VirtualKey.Down, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionDownMessage());
                break;
            case { Key: VirtualKey.PageUp, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionPageUpMessage());
                break;
            case { Key: VirtualKey.PageDown, Shift: true, Control: false, Alt: false }:
                Send(new ExtendSelectionPageDownMessage());
                break;
            case { Key: VirtualKey.Home, Shift: true, Alt: false }:
                Send(new ExtendSelectionHomeMessage());
                break;
            case { Key: VirtualKey.End, Shift: true, Alt: false }:
                Send(new ExtendSelectionEndMessage());
                break;

            case { Key: VirtualKey.Up, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorUpMessage());
                break;
            case { Key: VirtualKey.Down, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorDownMessage());
                break;
            case { Key: VirtualKey.PageUp, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorPageUpMessage());
                break;
            case { Key: VirtualKey.PageDown, Control: false, Shift: false, Alt: false }:
                Send(new MoveCursorPageDownMessage());
                break;
            case { Key: VirtualKey.Home, Shift: false, Alt: false }:
                Send(new MoveCursorHomeMessage());
                break;
            case { Key: VirtualKey.End, Shift: false, Alt: false }:
                Send(new MoveCursorEndMessage());
                break;
            case { Key: VirtualKey.Space, Shift: false, Alt: false }:
                Send(new ToggleSelectionAtCursorMessage());
                break;
            case { Key: VirtualKey.Insert, Control: false, Shift: false, Alt: false }:
                Send(new ToggleSelectionAtCursorAndAdvanceMessage());
                break;
            case { Key: VirtualKey.A, Control: true, Shift: false, Alt: false }:
                Send(new SelectAllMessage());
                break;
            case { Key: VirtualKey.A, Control: true, Shift: true, Alt: false }:
                Send(new ClearSelectionMessage());
                break;
            case { Key: VirtualKey.Escape, Control: false, Shift: false, Alt: false }:
                Send(new ClearSelectionMessage());
                break;
            case { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: false }:
                Send(new ActivateInvokedMessage());
                break;

            case { Key: VirtualKey.Back, Control: false, Shift: false, Alt: false }:
                Send(new NavigateUpKeyPressedMessage());
                break;
            case { Key: VirtualKey.PageUp, Control: true, Shift: false, Alt: false }:
                Send(new NavigateUpKeyPressedMessage());
                break;
            case { Key: VirtualKey.Up, Control: false, Shift: false, Alt: true }:
                Send(new NavigateUpKeyPressedMessage());
                break;
            case { Key: VirtualKey.F2, Control: false, Shift: false, Alt: false }:
                Send(new RenameKeyPressedMessage());
                break;
            case { Key: VirtualKey.F6, Control: false, Shift: true, Alt: false }:
                Send(new RenameKeyPressedMessage());
                break;
            case { Key: VirtualKey.Delete, Control: false, Alt: false }:
                Send(new DeleteKeyPressedMessage());
                break;
            case { Key: VirtualKey.F8, Control: false, Shift: false, Alt: false }:
                Send(new DeleteKeyPressedMessage());
                break;
            case { Key: VirtualKey.F5, Control: false, Shift: false, Alt: false }:
                Send(new CopyKeyPressedMessage());
                break;
            case { Key: VirtualKey.F6, Control: false, Shift: false, Alt: false }:
                Send(new MoveKeyPressedMessage());
                break;
            case { Key: VirtualKey.F7, Control: false, Shift: false, Alt: false }:
                Send(new CreateFolderKeyPressedMessage());
                break;
            case { Key: VirtualKey.N, Control: true, Shift: true, Alt: false }:
                Send(new CreateFolderKeyPressedMessage());
                break;
            case { Key: VirtualKey.C, Control: true, Shift: true, Alt: false }:
                Send(new CopyPathKeyPressedMessage());
                break;
            case { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: true }:
                Send(new PropertiesKeyPressedMessage());
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
            parameter is KeyboardInput input && owner.CanHandle(input);

        public void Execute(object? parameter)
        {
            if (parameter is KeyboardInput input)
            {
                owner.Handle(input);
            }
        }
    }
}
