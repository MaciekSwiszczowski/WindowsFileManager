using System.Reactive.Concurrency;

namespace WinUiFileManager.Presentation.FileEntryTable.Data;

public sealed class FileEntryTableDataSourceFactory
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IMessenger _messenger;

    public FileEntryTableDataSourceFactory(IFileSystemService fileSystemService, IMessenger messenger)
    {
        _fileSystemService = fileSystemService;
        _messenger = messenger;
    }

    internal FileEntryTableDataSource Create(string identity, IScheduler uiScheduler)
    {
        return new FileEntryTableDataSource(
            identity,
            uiScheduler,
            _fileSystemService,
            _messenger);
    }
}
