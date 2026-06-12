using WinUiFileManager.Presentation.Controls.FileInspector.Panel;
using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Inspector layout shown when exactly one file/folder is selected: renders the full set of diagnostic
/// categories for that single entry in an <see cref="AutoFillColumnsPanel"/>. Pure view bound to the shared
/// <see cref="InspectorViewModel"/>; its scroll-follow behaviour lives in
/// <see cref="Behaviors.InspectorStickyScrollBehavior"/>.
/// </summary>
public sealed partial class InspectorSingleSelectionView
{
    public InspectorSingleSelectionView()
    {
        InitializeComponent();
    }

    /// <summary>The bound inspector view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a VM is assigned.</exception>
    public InspectorViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorSingleSelectionView)} must be initialized with a view model.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }

    /// <summary>The scrolling host, exposed for <see cref="Behaviors.InspectorStickyScrollBehavior"/>.</summary>
    internal ScrollViewer ScrollHost => ContentScrollViewer;

    /// <summary>The category cards' content host (its size changes drive the bottom-follow), exposed for
    /// <see cref="Behaviors.InspectorStickyScrollBehavior"/>.</summary>
    internal FrameworkElement CategoriesHost => CategoriesItemsControl;
}
