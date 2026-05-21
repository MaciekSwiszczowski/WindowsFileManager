namespace WinUiFileManager.Application.FileEntries;

public interface IFileEntryDataReader
{
    IReadOnlyList<FileSystemEntryModel> GetEntries(
        NormalizedPath path,
        CancellationToken cancellationToken);

    FileSystemEntryModel? GetEntry(
        NormalizedPath path,
        CancellationToken cancellationToken);
}
