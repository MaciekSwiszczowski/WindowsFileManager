using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorSelectionFieldUpdater
{
    private readonly FileInspectorFieldState _fieldState;

    public FileInspectorSelectionFieldUpdater(FileInspectorFieldState fieldState)
    {
        _fieldState = fieldState;
    }

    public void HideAllFields()
    {
        _fieldState.HideAllFields();
    }

    public void ShowImmediateSingleSelectionFields(FileInspectorSelection selection)
    {
        BasicFileInspectorCategory.SetFields(selection, _fieldState);
        NtfsFileInspectorCategory.ApplyAttributes(selection.AttributesFlags, _fieldState);
        _fieldState.ShowDeferredFieldsAsLoading();
    }
}
