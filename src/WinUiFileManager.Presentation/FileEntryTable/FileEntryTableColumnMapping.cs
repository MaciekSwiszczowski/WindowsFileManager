
namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Two-way translation between a XAML column's <c>SortMemberPath</c> string and the strongly-typed
/// <see cref="SortColumn"/> used by the sort/data pipeline. Keeps the string-vs-enum mapping in one
/// place so the sorting behavior and column definitions cannot drift apart.
/// </summary>
internal static class FileEntryTableColumnMapping
{
    // The "Modified" column binds to LastWriteTime but uses a distinct member-path string in XAML.
    private const string ModifiedSortMemberPath = "Modified";

    /// <summary>Maps a column's <c>SortMemberPath</c> to a <see cref="SortColumn"/>, or null when the
    /// path does not correspond to a sortable file-entry column.</summary>
    public static SortColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(FileSystemEntryModel.Name) => SortColumn.Name,
            nameof(FileSystemEntryModel.Extension) => SortColumn.Extension,
            nameof(FileSystemEntryModel.Size) => SortColumn.Size,
            nameof(FileSystemEntryModel.LastWriteTime) or ModifiedSortMemberPath => SortColumn.Modified,
            nameof(FileSystemEntryModel.Attributes) => SortColumn.Attributes,
            _ => null,
        };

    /// <summary>Inverse of <see cref="MapColumn"/>: returns the <c>SortMemberPath</c> string for a
    /// <see cref="SortColumn"/>, defaulting to the name column.</summary>
    public static string MapSortMemberPath(SortColumn column) =>
        column switch
        {
            SortColumn.Name => nameof(FileSystemEntryModel.Name),
            SortColumn.Extension => nameof(FileSystemEntryModel.Extension),
            SortColumn.Size => nameof(FileSystemEntryModel.Size),
            SortColumn.Modified => ModifiedSortMemberPath,
            SortColumn.Attributes => nameof(FileSystemEntryModel.Attributes),
            _ => nameof(FileSystemEntryModel.Name),
        };
}
