namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeActivePanelsService : IActivePanelsService
{
    public string ActivePanelIdentity { get; private set; } = "Left";

    public string TargetPanelIdentity { get; private set; } = "Right";

    public void SetActivePanel(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || string.Equals(identity, ActivePanelIdentity, StringComparison.Ordinal))
        {
            return;
        }

        TargetPanelIdentity = ActivePanelIdentity;
        ActivePanelIdentity = identity;
    }
}
