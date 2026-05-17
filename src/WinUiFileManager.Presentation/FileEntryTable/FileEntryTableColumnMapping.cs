
namespace WinUiFileManager.Presentation.FileEntryTable;

internal static class FileEntryTableColumnMapping
{
    public static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(FileSystemEntryModel.Name) => FileEntryColumn.Name,
            nameof(FileSystemEntryModel.Extension) => FileEntryColumn.Extension,
            nameof(FileSystemEntryModel.Size) => FileEntryColumn.Size,
            nameof(FileSystemEntryModel.LastWriteTime) => FileEntryColumn.Modified,
            nameof(FileSystemEntryModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };

    public static string MapSortMemberPath(FileEntryColumn column) =>
        column switch
        {
            FileEntryColumn.Name => nameof(FileSystemEntryModel.Name),
            FileEntryColumn.Extension => nameof(FileSystemEntryModel.Extension),
            FileEntryColumn.Size => nameof(FileSystemEntryModel.Size),
            FileEntryColumn.Modified => nameof(FileSystemEntryModel.LastWriteTime),
            FileEntryColumn.Attributes => nameof(FileSystemEntryModel.Attributes),
            _ => nameof(FileSystemEntryModel.Name),
        };
}
