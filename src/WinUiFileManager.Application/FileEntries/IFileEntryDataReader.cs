namespace WinUiFileManager.Application.FileEntries;

public interface IFileEntryDataReader
{
    public IObservable<FileSystemEntryModel> GetEntries(NormalizedPath path, CancellationToken cancellationToken);

    public FileSystemEntryModel? GetEntry(NormalizedPath path, CancellationToken cancellationToken);
}
