using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileInspectorView
{
    public FileInspectorView() => InitializeComponent();

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
