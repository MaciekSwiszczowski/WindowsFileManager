using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.Controls.FileInspector;

/// <summary>
/// Renders a single read-only text inspector field (label + value) bound to an
/// <see cref="InspectorBasicFieldViewModel"/>, with a lightweight pointer-hover highlight on the row.
/// </summary>
/// <remarks>
/// The hover highlight is applied in code-behind (toggling the row <see cref="Border.Background"/> on
/// pointer enter/exit) rather than via a visual-state because the row is a plain <see cref="Border"/>;
/// the two brushes are cached as fields to avoid allocating a brush per hover. The pointer handlers are
/// wired in XAML, so there is nothing to unsubscribe in code-behind.
/// </remarks>
public sealed partial class InspectorTextFieldView
{
    // Cached hover/transparent brushes so hovering does not allocate a brush each time.
    private readonly Brush _rowHoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 96, 165, 250));
    private readonly Brush _rowTransparentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    public InspectorTextFieldView()
    {
        InitializeComponent();
    }

    /// <summary>The bound field view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before a field is assigned.</exception>
    public InspectorBasicFieldViewModel BasicField
    {
        get => field ?? throw new InvalidOperationException($"{nameof(InspectorTextFieldView)} must be initialized with a field.");
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
