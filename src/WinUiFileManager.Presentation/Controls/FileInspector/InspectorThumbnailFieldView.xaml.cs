using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

public sealed partial class InspectorThumbnailFieldView
{
    private readonly Brush _rowHoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 96, 165, 250));
    private readonly Brush _rowTransparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public InspectorThumbnailFieldView()
    {
        InitializeComponent();
    }

    public InspectorThumbnailFieldViewModel Field
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorThumbnailFieldView)} must be initialized with a field.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowHoverBrush;
        }
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowTransparentBrush;
        }
    }
}
