using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Maps <see cref="FileInspectorCategory"/> values to their human-readable section headers used in the inspector
/// UI and in search text. Centralizes the display names so they are not duplicated across views/view models.
/// </summary>
internal static class FileInspectorCategoryExtensions
{
    /// <summary>Returns the display header for a category, falling back to the enum name for unmapped values.</summary>
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
