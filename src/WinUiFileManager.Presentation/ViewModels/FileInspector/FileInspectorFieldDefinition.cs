namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed record FileInspectorFieldDefinition(
    FileInspectorCategory Category,
    string Key,
    string Tooltip,
    int SortOrder,
    bool IsDeferred = true);
