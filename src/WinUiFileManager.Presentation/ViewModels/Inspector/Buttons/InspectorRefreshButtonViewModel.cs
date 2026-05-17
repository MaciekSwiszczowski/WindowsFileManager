namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

public sealed class InspectorRefreshButtonViewModel
{
    private readonly IMessenger _messenger;

    public InspectorRefreshButtonViewModel(IMessenger messenger)
    {
        _messenger = messenger;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public IRelayCommand RefreshCommand { get; }

    private void Refresh()
    {
        _messenger.Send(new RefreshInspectorRequestMessage());
    }
}
