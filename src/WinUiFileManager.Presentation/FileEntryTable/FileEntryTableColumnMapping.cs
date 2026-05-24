
namespace WinUiFileManager.Presentation.FileEntryTable;

internal static class FileEntryTableColumnMapping
{
    private const string ModifiedSortMemberPath = "Modified";

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
