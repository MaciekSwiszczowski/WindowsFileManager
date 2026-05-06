namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class NtfsFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "NTFS";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("NTFS", "Created", "NTFS creation time in UTC.", 0, IsDeferred: false),
        new("NTFS", "Accessed", "NTFS last access time in UTC.", 1, IsDeferred: false),
        new("NTFS", "Modified", "NTFS last write time in UTC.", 2, IsDeferred: false),
        new("NTFS", "MFT Changed", "NTFS metadata change time in UTC.", 3, IsDeferred: false),
        new("NTFS", "Read Only", "Whether the item is marked read-only.", 4, IsDeferred: false),
        new("NTFS", "Hidden", "Whether the item is hidden.", 5, IsDeferred: false),
        new("NTFS", "System", "Whether the item is marked as a system file.", 6, IsDeferred: false),
        new("NTFS", "Archive", "Whether the archive attribute is set.", 7, IsDeferred: false),
        new("NTFS", "Temporary", "Whether the item is marked temporary.", 8, IsDeferred: false),
        new("NTFS", "Offline", "Whether the item is offline or placeholder-backed.", 9, IsDeferred: false),
        new("NTFS", "Not Content Indexed", "Whether the item should be excluded from content indexing.", 10, IsDeferred: false),
        new("NTFS", "Encrypted", "Whether the item is encrypted with EFS.", 11, IsDeferred: false),
        new("NTFS", "Compressed", "Whether the item is compressed by NTFS.", 12, IsDeferred: false),
        new("NTFS", "Sparse", "Whether the item is stored as a sparse file.", 13, IsDeferred: false),
        new("NTFS", "Reparse Point", "Whether the item is a reparse point.", 14, IsDeferred: false)
    ];
}
