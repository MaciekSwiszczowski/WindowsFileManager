namespace WinUiFileManager.Presentation.ViewModels;

public sealed class MessageDialogViewModel(string message)
{
    public string Message { get; } = message;
}
