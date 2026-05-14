using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileInspectorView
{
    private readonly Brush _rowHoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 96, 165, 250));
    private readonly Brush _rowTransparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public FileInspectorView()
    {
        InitializeComponent();
    }

    public InspectorViewModel ViewModel
    {
        get => field;
        set
        {
            field = value;
            DataContext = value;
            Bindings.Update();
        }
    } = new();

    private void OnInspectorRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowHoverBrush;
        }
    }

    private void OnInspectorRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowTransparentBrush;
        }
    }
}
