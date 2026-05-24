namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed class InspectorBasicFieldViewModel : InspectorFieldViewModelBase
{
    public delegate InspectorBasicFieldViewModel Factory(FileInspectorCategory category, string key, string tooltip, string value);

    public InspectorBasicFieldViewModel(FileInspectorCategory category, string key, string tooltip, string value = "") : base(category, key, tooltip, value)
    {
    }
}
