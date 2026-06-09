using WinUiFileManager.Application.Diagnostics.Profiling;
using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Maps <see cref="FileInspectorCategory"/> values to their human-readable section headers used in the inspector
/// UI and in search text. Centralizes the display names so they are not duplicated across views/view models.
/// </summary>
internal static class FileInspectorCategoryExtensions
{
    /// <summary>
    /// Maps a visual category to the diagnostics handler that feeds it, or <see langword="null"/> when the category
    /// has no deferred handler (Basic). NTFS and Ids are both fed by the Identity handler.
    /// </summary>
    public static DiagnosticsCategory? ToDiagnosticsCategory(this FileInspectorCategory category) => category switch
    {
        Ntfs => DiagnosticsCategory.Identity,
        Ids => DiagnosticsCategory.Identity,
        Locks => DiagnosticsCategory.Locks,
        Links => DiagnosticsCategory.Links,
        Streams => DiagnosticsCategory.Streams,
        Security => DiagnosticsCategory.Security,
        Thumbnails => DiagnosticsCategory.Thumbnail,
        Cloud => DiagnosticsCategory.Cloud,
        _ => null,
    };

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
