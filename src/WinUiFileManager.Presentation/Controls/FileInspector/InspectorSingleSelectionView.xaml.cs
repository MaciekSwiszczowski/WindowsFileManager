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
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorSingleSelectionView)} must be initialized with a view model.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }
}
