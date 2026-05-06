namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal interface IFileInspectorCategoryProvider
{
    FileInspectorCategory Category { get; }

    IReadOnlyList<FileInspectorFieldDefinition> Fields { get; }
}
