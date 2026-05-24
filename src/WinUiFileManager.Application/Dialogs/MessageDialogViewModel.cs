namespace WinUiFileManager.Application.Dialogs;

public sealed class MessageDialogViewModel(string message)
{
    public delegate MessageDialogViewModel Factory(string message);

    public string Message { get; } = message;
}
