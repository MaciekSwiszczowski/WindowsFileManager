namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Tracks which pane currently has focus in the dual-pane shell, so commands (copy, move, etc.) know
/// their source and destination. Implemented in Infrastructure; consumed by command/handler code.
/// </summary>
public interface IActivePanelsService
{
    /// <summary>Identity of the pane the user is currently acting in (the command source).</summary>
    public string ActivePanelIdentity { get; }

    /// <summary>Identity of the opposite pane (the default destination for copy/move).</summary>
    public string TargetPanelIdentity { get; }

    /// <summary>Records <paramref name="identity"/> as the active pane; the target is derived as the other pane.</summary>
    public void SetActivePanel(string identity);
}
