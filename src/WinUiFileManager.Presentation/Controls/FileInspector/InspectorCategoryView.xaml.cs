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
        get => field;
        set
        {
            field = value;
            Bindings.Update();
        }
    } = new(FileInspectorCategory.Basic);
}
