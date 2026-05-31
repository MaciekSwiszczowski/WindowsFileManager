namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// The grouping categories shown in the file inspector. Declaration order is also the display order of the
/// category sections. Each value maps to a display name via <see cref="FileInspectorCategoryExtensions.GetDisplayName"/>.
/// </summary>
public enum FileInspectorCategory
{
    /// <summary>Name, path, type, size and attribute basics.</summary>
    Basic,

    /// <summary>NTFS timestamps and attribute flags.</summary>
    Ntfs,

    /// <summary>NTFS identity values (file ID, volume serial, hard-link count, final path).</summary>
    Ids,

    /// <summary>Lock / in-use diagnostics (processes, services, PIDs).</summary>
    Locks,

    /// <summary>Symbolic-link, junction, shortcut and reparse-point details.</summary>
    Links,

    /// <summary>Alternate data stream count and names.</summary>
    Streams,

    /// <summary>Owner/group and ACL (security descriptor) summary.</summary>
    Security,

    /// <summary>Shell thumbnail preview and availability.</summary>
    Thumbnails,

    /// <summary>Cloud / placeholder (sync-root) state.</summary>
    Cloud,
}
