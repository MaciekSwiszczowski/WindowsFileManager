namespace WinUiFileManager.Application.FileEntries;

/// <summary>Identifies the file-table column a pane is sorted by; persisted as part of <see cref="WinUiFileManager.Application.Settings.SortState"/>.</summary>
public enum SortColumn
{
    /// <summary>Sort by entry name.</summary>
    Name,

    /// <summary>Sort by file extension.</summary>
    Extension,

    /// <summary>Sort by size in bytes.</summary>
    Size,

    /// <summary>Sort by last-write (modified) time.</summary>
    Modified,

    /// <summary>Sort by NTFS attribute flags.</summary>
    Attributes
}
