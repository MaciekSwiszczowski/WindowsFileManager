
namespace WinUiFileManager.Presentation.FileEntryTable;

internal static class FileEntryTableColumnMapping
{
    private const string ModifiedSortMemberPath = "Modified";

    public static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(FileSystemEntryModel.Name) => FileEntryColumn.Name,
            nameof(FileSystemEntryModel.Extension) => FileEntryColumn.Extension,
            nameof(FileSystemEntryModel.Size) => FileEntryColumn.Size,
            nameof(FileSystemEntryModel.LastWriteTime) or ModifiedSortMemberPath => FileEntryColumn.Modified,
            nameof(FileSystemEntryModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };

    public static SortColumn? MapSortColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(FileSystemEntryModel.Name) => SortColumn.Name,
            nameof(FileSystemEntryModel.Extension) => SortColumn.Extension,
            nameof(FileSystemEntryModel.Size) => SortColumn.Size,
            nameof(FileSystemEntryModel.LastWriteTime) or ModifiedSortMemberPath => SortColumn.Modified,
            nameof(FileSystemEntryModel.Attributes) => SortColumn.Attributes,
            _ => null,
        };

    public static string MapSortMemberPath(FileEntryColumn column) =>
        column switch
        {
            FileEntryColumn.Name => nameof(FileSystemEntryModel.Name),
            FileEntryColumn.Extension => nameof(FileSystemEntryModel.Extension),
            FileEntryColumn.Size => nameof(FileSystemEntryModel.Size),
            FileEntryColumn.Modified => ModifiedSortMemberPath,
            FileEntryColumn.Attributes => nameof(FileSystemEntryModel.Attributes),
            _ => nameof(FileSystemEntryModel.Name),
        };
}
