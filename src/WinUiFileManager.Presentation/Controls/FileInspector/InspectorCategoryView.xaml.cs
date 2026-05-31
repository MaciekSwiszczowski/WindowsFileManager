using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Renders one collapsible inspector category (a titled group of fields) by binding to its
/// <see cref="InspectorCategoryViewModel"/>. Pure view.
/// </summary>
public sealed partial class InspectorCategoryView
{
    public InspectorCategoryView()
    {
        InitializeComponent();
    }

    /// <summary>The bound category view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a category is assigned.</exception>
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
