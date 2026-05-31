namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

/// <summary>
/// View model for the inspector's refresh button. Sends a <see cref="RefreshInspectorRequestMessage"/> so the
/// active pane re-emits its selection and the inspector reloads. Stateless; holds no messenger registrations
/// (it only sends), so nothing to dispose.
/// </summary>
public sealed class InspectorRefreshButtonViewModel
{
    private readonly IMessenger _messenger;

    public InspectorRefreshButtonViewModel(IMessenger messenger)
    {
        _messenger = messenger;
        RefreshCommand = new RelayCommand(Refresh);
    }

    /// <summary>Command bound to the refresh button.</summary>
    public IRelayCommand RefreshCommand { get; }

    /// <summary>Broadcasts a refresh request to repopulate the inspector from the current selection.</summary>
    private void Refresh()
    {
        _messenger.Send(new RefreshInspectorRequestMessage());
    }
}
