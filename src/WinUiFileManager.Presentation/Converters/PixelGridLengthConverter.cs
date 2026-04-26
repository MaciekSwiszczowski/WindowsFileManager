using Microsoft.UI.Xaml.Data;

namespace WinUiFileManager.Presentation.Converters;

public sealed class PixelGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double width
            ? new GridLength(width, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is GridLength width && width.GridUnitType == GridUnitType.Pixel
            ? width.Value
            : 0d;
}
