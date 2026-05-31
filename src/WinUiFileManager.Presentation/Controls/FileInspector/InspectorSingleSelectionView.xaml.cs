using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Inspector layout shown when exactly one file/folder is selected: renders the full set of diagnostic
/// categories for that single entry. Pure view bound to the shared <see cref="InspectorViewModel"/>.
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
}
