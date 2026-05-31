using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Renders a single toggle (boolean) inspector field bound to an
/// <see cref="InspectorToggleFieldViewModel"/>, with the same pointer-hover row highlight as the other
/// inspector field views.
/// </summary>
/// <remarks>
/// Hover brushes are cached as fields to avoid per-hover allocation; the pointer handlers are wired in
/// XAML so there is nothing to unsubscribe here.
/// </remarks>
public sealed partial class InspectorToggleFieldView
{
    // Cached hover/transparent brushes so hovering does not allocate a brush each time.
    private readonly Brush _rowHoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 96, 165, 250));
    private readonly Brush _rowTransparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public InspectorToggleFieldView()
    {
        InitializeComponent();
    }

    /// <summary>The bound field view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a field is assigned.</exception>
    public InspectorToggleFieldViewModel Field
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorToggleFieldView)} must be initialized with a field.");
        set
        {
            field = value;
            Bindings.Update();
        }
    }

    // Apply the hover highlight while the pointer is over the row.
    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowHoverBrush;
        }
    }

    // Restore the transparent background when the pointer leaves the row.
    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border row)
        {
            row.Background = _rowTransparentBrush;
        }
    }
}
