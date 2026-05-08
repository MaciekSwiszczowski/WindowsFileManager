using System.Reactive.Concurrency;

namespace WinUiFileManager.Presentation.FileEntryTable.Data;

public sealed class FileEntryTableDataSourceFactory
{
    private readonly IFileSystemService _fileSystemService;

    public FileEntryTableDataSourceFactory(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    internal FileEntryTableDataSource Create(string identity, string initialPath, IScheduler uiScheduler)
    {
        return new FileEntryTableDataSource(
            identity,
            initialPath,
            uiScheduler,
            _fileSystemService);
    }
}
