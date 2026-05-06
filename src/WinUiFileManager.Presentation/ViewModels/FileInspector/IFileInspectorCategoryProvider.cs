namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal interface IFileInspectorCategoryProvider
{
    string Category { get; }

    IReadOnlyList<FileInspectorFieldDefinition> Fields { get; }
}
