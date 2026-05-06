namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal static class FileInspectorFormatting
{
    public static string RequiredUtc(DateTime value) =>
        value == DateTime.MinValue
            ? "Unavailable"
            : value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    public static string OptionalBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => string.Empty
        };
}
