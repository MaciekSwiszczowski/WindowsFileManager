namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed class InspectorBasicFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorBasicFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
    }
}
