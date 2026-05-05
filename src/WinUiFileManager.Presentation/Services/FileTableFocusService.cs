namespace WinUiFileManager.Presentation.Services;

public sealed class FileTableFocusService
{
    private const string LeftPanelIdentity = "Left";
    private const string RightPanelIdentity = "Right";

    private string _activePanelIdentity = string.Empty;

    public FileTableFocusService()
    {
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    public event EventHandler? ActivePanelChanged;

    public string ActivePanelIdentity => _activePanelIdentity;

    public string TargetPanelIdentity =>
        _activePanelIdentity switch
        {
            LeftPanelIdentity => RightPanelIdentity,
            RightPanelIdentity => LeftPanelIdentity,
            _ => string.Empty
        };

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused && _activePanelIdentity != message.Identity)
        {
            _activePanelIdentity = message.Identity;
            ActivePanelChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
