using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls;

/// <summary>
/// Root control for the file inspector pane. It binds to the <see cref="InspectorViewModel"/> and (via
/// XAML) switches between the no-/single-/multi-selection child views based on the inspector's current
/// selection mode. Pure view: it holds no state beyond the bound VM.
/// </summary>
public sealed partial class FileInspectorView
{
    public FileInspectorView() => InitializeComponent();

    /// <summary>The bound inspector view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a VM is assigned.</exception>
    public InspectorViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(FileInspectorView)} must be initialized with a view model.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }
}
