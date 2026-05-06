namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal static class FileInspectorCategorySort
{
    public static int GetSortOrder(string category) => category switch
    {
        "Basic" => 0,
        "NTFS" => 1,
        "IDs" => 2,
        "Locks" => 3,
        "Links" => 4,
        "Streams" => 5,
        "Security" => 6,
        "Thumbnails" => 7,
        "Cloud" => 8,
        _ => 99
    };
}
