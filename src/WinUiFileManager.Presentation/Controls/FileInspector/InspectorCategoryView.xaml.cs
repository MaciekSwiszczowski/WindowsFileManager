using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

public sealed partial class InspectorCategoryView
{
    public InspectorCategoryView()
    {
        InitializeComponent();
    }

    public InspectorCategoryViewModel Category
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorCategoryView)} must be initialized with a category.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }
}
