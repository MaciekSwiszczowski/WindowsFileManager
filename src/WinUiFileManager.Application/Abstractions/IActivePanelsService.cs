namespace WinUiFileManager.Application.Abstractions;

public interface IActivePanelsService
{
    public string ActivePanelIdentity { get; }

    public string TargetPanelIdentity { get; }

    public void SetActivePanel(string identity);
}
