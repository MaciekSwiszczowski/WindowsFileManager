using Microsoft.UI.Xaml.Data;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.Converters;

public sealed class FileAttributesToTableTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is FileAttributes attributes
            ? FileEntryDisplayStringCache.Shared.GetTableAttributes(attributes)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
