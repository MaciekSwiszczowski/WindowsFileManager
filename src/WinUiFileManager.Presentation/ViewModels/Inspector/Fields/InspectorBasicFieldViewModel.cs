namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// The default plain-text inspector field. Adds no behavior beyond <see cref="InspectorFieldViewModelBase"/>;
/// it exists so the field factory can create concrete text fields.
/// </summary>
public sealed class InspectorBasicFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorBasicFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
    }
}
