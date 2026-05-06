using System.Collections.Frozen;
using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class NtfsFileInspectorCategory : IFileInspectorCategoryProvider
{
    private static readonly FrozenDictionary<string, FileAttributes> ToggleableFlags =
        new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase)
        {
            ["Read Only"] = FileAttributes.ReadOnly,
            ["Hidden"] = FileAttributes.Hidden,
            ["Archive"] = FileAttributes.Archive,
            ["Temporary"] = FileAttributes.Temporary,
            ["Not Content Indexed"] = FileAttributes.NotContentIndexed
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public FileInspectorCategory Category => Ntfs;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Ntfs, "Created", "NTFS creation time in UTC.", 0, IsDeferred: false),
        new(Ntfs, "Accessed", "NTFS last access time in UTC.", 1, IsDeferred: false),
        new(Ntfs, "Modified", "NTFS last write time in UTC.", 2, IsDeferred: false),
        new(Ntfs, "MFT Changed", "NTFS metadata change time in UTC.", 3, IsDeferred: false),
        new(Ntfs, "Read Only", "Whether the item is marked read-only.", 4, IsDeferred: false),
        new(Ntfs, "Hidden", "Whether the item is hidden.", 5, IsDeferred: false),
        new(Ntfs, "System", "Whether the item is marked as a system file.", 6, IsDeferred: false),
        new(Ntfs, "Archive", "Whether the archive attribute is set.", 7, IsDeferred: false),
        new(Ntfs, "Temporary", "Whether the item is marked temporary.", 8, IsDeferred: false),
        new(Ntfs, "Offline", "Whether the item is offline or placeholder-backed.", 9, IsDeferred: false),
        new(Ntfs, "Not Content Indexed", "Whether the item should be excluded from content indexing.", 10, IsDeferred: false),
        new(Ntfs, "Encrypted", "Whether the item is encrypted with EFS.", 11, IsDeferred: false),
        new(Ntfs, "Compressed", "Whether the item is compressed by NTFS.", 12, IsDeferred: false),
        new(Ntfs, "Sparse", "Whether the item is stored as a sparse file.", 13, IsDeferred: false),
        new(Ntfs, "Reparse Point", "Whether the item is a reparse point.", 14, IsDeferred: false)
    ];

    public static bool CanToggleField(string key) => ToggleableFlags.ContainsKey(key);

    public static bool TryGetToggleFlag(string key, out FileAttributes flag) =>
        ToggleableFlags.TryGetValue(key, out flag);

    public static void ApplyAttributes(FileAttributes attributes, FileInspectorFieldStore fields)
    {
        fields.SetValue("Read Only", FormatFlag(attributes.HasFlag(FileAttributes.ReadOnly)));
        fields.SetValue("Hidden", FormatFlag(attributes.HasFlag(FileAttributes.Hidden)));
        fields.SetValue("System", FormatFlag(attributes.HasFlag(FileAttributes.System)));
        fields.SetValue("Archive", FormatFlag(attributes.HasFlag(FileAttributes.Archive)));
        fields.SetValue("Temporary", FormatFlag(attributes.HasFlag(FileAttributes.Temporary)));
        fields.SetValue("Offline", FormatFlag(attributes.HasFlag(FileAttributes.Offline)));
        fields.SetValue("Not Content Indexed", FormatFlag(attributes.HasFlag(FileAttributes.NotContentIndexed)));
        fields.SetValue("Encrypted", FormatFlag(attributes.HasFlag(FileAttributes.Encrypted)));
        fields.SetValue("Compressed", FormatFlag(attributes.HasFlag(FileAttributes.Compressed)));
        fields.SetValue("Sparse", FormatFlag(attributes.HasFlag(FileAttributes.SparseFile)));
        fields.SetValue("Reparse Point", FormatFlag(attributes.HasFlag(FileAttributes.ReparsePoint)));
    }

    private static string FormatFlag(bool value) => value ? "Yes" : "No";
}
