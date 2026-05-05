namespace WinUiFileManager.Presentation.Services;

public sealed partial class FileTableFocusService : ObservableObject
{
    [ObservableProperty]
    public partial string ActivePanelIdentity { get; private set; } = string.Empty;

    public FileTableFocusService()
    {
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            ActivePanelIdentity = message.Identity;
        }
    }
}
