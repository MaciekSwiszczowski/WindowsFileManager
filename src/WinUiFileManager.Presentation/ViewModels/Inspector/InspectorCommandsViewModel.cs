namespace WinUiFileManager.Presentation.ViewModels.Inspector;

public sealed class InspectorCommandsViewModel
{
    public InspectorCommandsViewModel()
    {
        RefreshCommand = new RelayCommand(static () => { });
        ShowPropertiesCommand = new RelayCommand(static () => { });
        CopyAllCommand = new RelayCommand(static () => { });
    }

    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand ShowPropertiesCommand { get; }

    public IRelayCommand CopyAllCommand { get; }
}
