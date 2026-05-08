namespace WinUiFileManager.Presentation.Services;

public sealed class ActivePanelsService
{
    private string _activePanelIdentity = "Left";
    private string _targetPanelIdentity = "Right";

    public ActivePanelsService()
    {
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    public string ActivePanelIdentity => _activePanelIdentity;

    public string TargetPanelIdentity => _targetPanelIdentity;

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            _activePanelIdentity = message.Identity;
        }
        else
        {
            _targetPanelIdentity = message.Identity;
        }
    }
}
