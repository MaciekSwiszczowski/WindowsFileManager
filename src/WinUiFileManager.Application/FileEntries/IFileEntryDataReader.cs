namespace WinUiFileManager.Application.FileEntries;

public interface IFileEntryDataReader
{
    IObservable<FileSystemEntryModel> GetEntries(NormalizedPath path,
        CancellationToken cancellationToken);

    FileSystemEntryModel? GetEntry(
        NormalizedPath path,
        CancellationToken cancellationToken);
}
