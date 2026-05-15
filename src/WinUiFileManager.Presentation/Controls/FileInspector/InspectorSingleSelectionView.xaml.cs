using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

public sealed partial class InspectorSingleSelectionView
{
    public InspectorSingleSelectionView()
    {
        InitializeComponent();
    }

    public InspectorViewModel ViewModel
    {
        get => field;
        set
        {
            field = value;
            Bindings.Update();
        }
    } = new();
}
