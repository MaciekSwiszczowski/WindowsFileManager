namespace WinUiFileManager.Presentation.FileEntryTable;

public static class SpecFileEntryDisplay
{
    public static string GetName(FileSystemEntryModel? model) =>
        model?.Name ?? "..";

    public static string GetExtension(FileSystemEntryModel? model) =>
        model?.Extension ?? string.Empty;

    public static string GetModified(FileSystemEntryModel? model) =>
        model is null
            ? string.Empty
            : model.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static string GetAttributes(FileSystemEntryModel? model) =>
        model?.Attributes.ToString() ?? string.Empty;
}
