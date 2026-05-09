namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelsViewModel : ObservableObject, IDisposable
{
    private readonly IActivePanelsService _activePanelsService;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public PanelsViewModel(
        IActivePanelsService activePanelsService,
        IMessenger messenger)
    {
        _activePanelsService = activePanelsService;
        _messenger = messenger;
        _messenger.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
        _messenger.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
        SetActivePanel(_activePanelsService.ActivePanelIdentity);
    }

    public PanelViewModel LeftPanel { get; } = new("Left");

    public PanelViewModel RightPanel { get; } = new("Right");

    [ObservableProperty]
    public partial double LeftPanelWidth { get; set; } = 600d;

    public string ActivePanelIdentity => _activePanelsService.ActivePanelIdentity;

    public PanelViewModel ActivePanel => GetPanel(ActivePanelIdentity);

    public IMessenger Messenger => _messenger;

    public void SetActivePanel(string identity)
    {
        var panel = GetPanel(identity);
        _activePanelsService.SetActivePanel(panel.Identity);
        OnPropertyChanged(nameof(ActivePanelIdentity));
        OnPropertyChanged(nameof(ActivePanel));
        LeftPanel.IsActive = string.Equals(LeftPanel.Identity, ActivePanelIdentity, StringComparison.Ordinal);
        RightPanel.IsActive = string.Equals(RightPanel.Identity, ActivePanelIdentity, StringComparison.Ordinal);
    }

    public PanelViewModel GetPanel(string identity) =>
        string.Equals(identity, RightPanel.Identity, StringComparison.OrdinalIgnoreCase)
            ? RightPanel
            : LeftPanel;

    public PanelViewModel GetOtherPanel() =>
        string.Equals(ActivePanelIdentity, LeftPanel.Identity, StringComparison.Ordinal)
            ? RightPanel
            : LeftPanel;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            SetActivePanel(message.Identity);
        }
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        GetPanel(message.Identity).SelectedCount = message.SelectedItems.Count;
    }
}
