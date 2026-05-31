using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Inspector layout shown when multiple entries are selected: renders the aggregate/summary view for the
/// selection rather than per-entry diagnostics. Pure view bound to the shared <see cref="InspectorViewModel"/>.
/// </summary>
public sealed partial class InspectorMultiSelectionView
{

    public InspectorMultiSelectionView()
    {
        InitializeComponent();
    }

    /// <summary>The bound inspector view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a VM is assigned.</exception>
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
