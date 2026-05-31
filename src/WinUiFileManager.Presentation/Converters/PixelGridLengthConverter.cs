using Microsoft.UI.Xaml.Data;

namespace WinUiFileManager.Presentation.Converters;

/// <summary>
/// XAML two-way value converter between a pixel width as a <see cref="double"/> and a pixel
/// <see cref="GridLength"/>. Used to bind a VM-stored pixel width (e.g. the inspector column width) to a
/// <see cref="ColumnDefinition.Width"/>, which is typed as <see cref="GridLength"/>.
/// </summary>
/// <remarks>Input: <c>double</c> ↔ output: pixel <see cref="GridLength"/>. Non-double / non-pixel inputs
/// degrade to 0 rather than throwing, so a missing binding does not crash layout.</remarks>
public sealed class PixelGridLengthConverter : IValueConverter
{
    /// <summary>Converts a <see cref="double"/> width to a pixel <see cref="GridLength"/>; non-double
    /// values become a zero-pixel length.</summary>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double width
            ? new GridLength(width, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

    /// <summary>Converts a pixel <see cref="GridLength"/> back to its <see cref="double"/> width; star/
    /// auto lengths (no pixel value) become 0.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is GridLength width && width.GridUnitType == GridUnitType.Pixel
            ? width.Value
            : 0d;
}
