using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

public sealed partial class InspectorMultiSelectionView
{

    public InspectorMultiSelectionView()
    {
        InitializeComponent();
    }

    public InspectorViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorMultiSelectionView)} must be initialized with a view model.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }
}
