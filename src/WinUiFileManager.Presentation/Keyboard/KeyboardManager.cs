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

    private bool CanHandle(KeyboardInput input) =>
        CreateMessage(input) is not null;

    private void Handle(KeyboardInput input)
    {
        var message = CreateMessage(input);
        if (message is not null)
        {
            _messenger.Send(message);
        }
    }

    private static object? CreateMessage(KeyboardInput input) =>
        input switch
        {
            { Key: VirtualKey.Up, Shift: true, Control: false, Alt: false } => new ExtendSelectionUpMessage(),
            { Key: VirtualKey.Down, Shift: true, Control: false, Alt: false } => new ExtendSelectionDownMessage(),
            { Key: VirtualKey.PageUp, Shift: true, Control: false, Alt: false } => new ExtendSelectionPageUpMessage(),
            { Key: VirtualKey.PageDown, Shift: true, Control: false, Alt: false } => new ExtendSelectionPageDownMessage(),
            { Key: VirtualKey.Home, Shift: true, Alt: false } => new ExtendSelectionHomeMessage(),
            { Key: VirtualKey.End, Shift: true, Alt: false } => new ExtendSelectionEndMessage(),

            { Key: VirtualKey.Up, Control: false, Shift: false, Alt: false } => new MoveCursorUpMessage(),
            { Key: VirtualKey.Down, Control: false, Shift: false, Alt: false } => new MoveCursorDownMessage(),
            { Key: VirtualKey.PageUp, Control: false, Shift: false, Alt: false } => new MoveCursorPageUpMessage(),
            { Key: VirtualKey.PageDown, Control: false, Shift: false, Alt: false } => new MoveCursorPageDownMessage(),
            { Key: VirtualKey.Home, Shift: false, Alt: false } => new MoveCursorHomeMessage(),
            { Key: VirtualKey.End, Shift: false, Alt: false } => new MoveCursorEndMessage(),
            { Key: VirtualKey.Space, Shift: false, Alt: false } => new ToggleSelectionAtCursorMessage(),
            { Key: VirtualKey.Insert, Control: false, Shift: false, Alt: false } => new ToggleSelectionAtCursorAndAdvanceMessage(),
            { Key: VirtualKey.A, Control: true, Shift: false, Alt: false } => new SelectAllMessage(),
            { Key: VirtualKey.A, Control: true, Shift: true, Alt: false } => new ClearSelectionMessage(),
            { Key: VirtualKey.Escape, Control: false, Shift: false, Alt: false } => new ClearSelectionMessage(),
            { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: false } => new ActivateInvokedMessage(),

            { Key: VirtualKey.Back, Control: false, Shift: false, Alt: false } => new NavigateUpKeyPressedMessage(),
            { Key: VirtualKey.PageUp, Control: true, Shift: false, Alt: false } => new NavigateUpKeyPressedMessage(),
            { Key: VirtualKey.Up, Control: false, Shift: false, Alt: true } => new NavigateUpKeyPressedMessage(),
            { Key: VirtualKey.F2, Control: false, Shift: false, Alt: false } => new RenameKeyPressedMessage(),
            { Key: VirtualKey.F6, Control: false, Shift: true, Alt: false } => new RenameKeyPressedMessage(),
            { Key: VirtualKey.Delete, Control: false, Alt: false } => new DeleteKeyPressedMessage(),
            { Key: VirtualKey.F8, Control: false, Shift: false, Alt: false } => new DeleteKeyPressedMessage(),
            { Key: VirtualKey.F5, Control: false, Shift: false, Alt: false } => new CopyKeyPressedMessage(),
            { Key: VirtualKey.F6, Control: false, Shift: false, Alt: false } => new MoveKeyPressedMessage(),
            { Key: VirtualKey.F7, Control: false, Shift: false, Alt: false } => new CreateFolderKeyPressedMessage(),
            { Key: VirtualKey.N, Control: true, Shift: true, Alt: false } => new CreateFolderKeyPressedMessage(),
            { Key: VirtualKey.C, Control: true, Shift: true, Alt: false } => new CopyPathKeyPressedMessage(),
            { Key: VirtualKey.Enter, Control: false, Shift: false, Alt: true } => new PropertiesKeyPressedMessage(),
            _ => null,
        };

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
