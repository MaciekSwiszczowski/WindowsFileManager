namespace WinUiFileManager.Presentation.FileEntryTable;

public static class SpecFileEntryDisplay
{
    public static string GetName(FileSystemEntryModel? model) => model?.Name ?? "..";

    public static string GetModified(FileSystemEntryModel? model) =>
        model is null
            ? string.Empty
            : model.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
}
