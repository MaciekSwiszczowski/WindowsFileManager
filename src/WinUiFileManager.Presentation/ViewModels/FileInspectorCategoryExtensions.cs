using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels;

internal static class FileInspectorCategoryExtensions
{
    public static string GetDisplayName(this FileInspectorCategory category) => category switch
    {
        Basic => "Basic",
        Ntfs => "NTFS",
        Ids => "IDs",
        Locks => "Locks",
        Links => "Links",
        Streams => "Streams",
        Security => "Security",
        Thumbnails => "Thumbnails",
        Cloud => "Cloud",
        _ => category.ToString()
    };
}
