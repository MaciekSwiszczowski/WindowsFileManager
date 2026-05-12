namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileInspectorView
{
    public FileInspectorView()
    {
        InitializeComponent();
    }

    public FileInspectorViewModel? ViewModel
    {
        get;
        set
        {
            field = value;
            DataContext = value;
        }
    }
}
