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
        ?? throw new InvalidOperationException($"{nameof(Field)} must be a thumbnail field.");

    public InspectorToggleFieldViewModel ToggleField => Field as InspectorToggleFieldViewModel
                                                        ?? throw new InvalidOperationException($"{nameof(Field)} must be a toggle field.");
}
