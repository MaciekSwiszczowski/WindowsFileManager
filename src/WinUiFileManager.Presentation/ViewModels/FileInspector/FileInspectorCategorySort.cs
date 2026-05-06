using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal static class FileInspectorCategorySort
{
    public static int GetSortOrder(FileInspectorCategory category) => category switch
    {
        Basic => 0,
        Ntfs => 1,
        Ids => 2,
        Locks => 3,
        Links => 4,
        Streams => 5,
        Security => 6,
        Thumbnails => 7,
        Cloud => 8,
        _ => 99
    };
}
