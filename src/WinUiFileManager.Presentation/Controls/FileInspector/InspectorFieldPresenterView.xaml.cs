using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

public sealed partial class InspectorFieldPresenterView
{
    public InspectorFieldPresenterView()
    {
        InitializeComponent();
    }

    public InspectorFieldViewModel Field
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorFieldPresenterView)} must be initialized with a field.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }

    public InspectorThumbnailFieldViewModel ThumbnailField =>
        Field as InspectorThumbnailFieldViewModel
        ?? new InspectorThumbnailFieldViewModel(Field.Category, Field.Key, Field.Tooltip, Field.Value);

    public InspectorToggleFieldViewModel ToggleField =>
        Field as InspectorToggleFieldViewModel
        ?? new InspectorToggleFieldViewModel(Field.Category, Field.Key, Field.Tooltip, Field.Value);
}
