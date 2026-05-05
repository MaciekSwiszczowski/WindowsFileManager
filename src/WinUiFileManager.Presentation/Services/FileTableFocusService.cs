namespace WinUiFileManager.Presentation.Services;

public sealed class FileTableFocusService
{
    private string _sourcePanelIdentity = string.Empty;
    private string _targetPanelIdentity = string.Empty;

    public FileTableFocusService()
    {
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    public string SourcePanelIdentity => _sourcePanelIdentity;

    public string TargetPanelIdentity => _targetPanelIdentity;

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            _sourcePanelIdentity = message.Identity;
        }
        else
        {
            _targetPanelIdentity = message.Identity;
        }
    }
}
