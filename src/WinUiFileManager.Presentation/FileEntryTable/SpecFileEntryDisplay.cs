namespace WinUiFileManager.Presentation.FileEntryTable;

public static class SpecFileEntryDisplay
{
    private static readonly string[] SizeSuffixes = ["B", "KB", "MB", "GB", "TB"];

    public static string GetName(FileSystemEntryModel? model) =>
        model?.Name ?? "..";

    public static string GetExtension(FileSystemEntryModel? model) =>
        model?.Extension ?? string.Empty;

    public static string GetSize(FileSystemEntryModel? model)
    {
        if (model is null || model.Kind == ItemKind.Directory)
        {
            return string.Empty;
        }

        return FormatSize(model.Size);
    }

    public static string GetModified(FileSystemEntryModel? model) =>
        model is null
            ? string.Empty
            : model.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static string GetAttributes(FileSystemEntryModel? model) =>
        model?.Attributes.ToString() ?? string.Empty;

    private static string FormatSize(long bytes)
    {
        var suffixIndex = 0;
        var size = (double)bytes;

        while (size >= 1024 && suffixIndex < SizeSuffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{size:F0} {SizeSuffixes[suffixIndex]}"
            : $"{size:F2} {SizeSuffixes[suffixIndex]}";
    }
}
