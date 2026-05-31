namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed record InspectorFieldCreationRequest(
    FileInspectorCategory Category,
    string Key,
    string Tooltip,
    string Value);
