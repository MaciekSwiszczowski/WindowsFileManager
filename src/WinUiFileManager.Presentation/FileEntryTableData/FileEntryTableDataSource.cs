using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public sealed class FileEntryTableDataSource : IDisposable
{
    private readonly CompositeDisposable _disposables;
    private readonly IFolderEntryScanner _folderEntryScanner;
    private readonly IFileEntryRowReader _fileEntryRowReader;
    private readonly CancellationTokenSource _scanCancellation = new();
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private bool _disposed;
    private readonly NormalizedPath _folderPath;
    private readonly Identity _identity;

    public FileEntryTableDataSource(
        string identity,
        NormalizedPath folderPath,
        ISchedulerProvider schedulers,
        IFolderEntryScanner folderEntryScanner,
        IFileEntryRowReader fileEntryRowReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger,
        FileEntryDisplayStringCache displayStringCache)
    {
        _identity = identity;
        _folderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        _folderEntryScanner = folderEntryScanner;
        _fileEntryRowReader = fileEntryRowReader;
        _displayStringCache = displayStringCache;

        _disposables = Initialize(schedulers.MainThread, directoryChangeStream, messenger);
        _disposables.Add(_scanCancellation);
    }

    public string CurrentPath { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];


    private CompositeDisposable Initialize(
        IScheduler uiScheduler,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        var rows = new SourceCache<SpecFileEntryViewModel, FilePathKey>(static item => item.GetKey());
        rows.AddOrUpdate(_folderEntryScanner.Scan(_folderPath, _scanCancellation.Token));

        var sortComparers = messenger
            .CreateObservable<FileTableSortRequestedMessage>()
            .Where(message => message.Identity == _identity)
            .Select(message => CreateComparer(new SortState(message.Column, message.Ascending)))
            .StartWith(CreateComparer(SortState.Default))
            .ObserveOn(uiScheduler);

        CompositeDisposable disposables =
        [
            directoryChangeStream
                .Watch(_folderPath)
                .Subscribe(change => ApplyDirectoryChange(rows, change)),
            rows.Connect()
                .ObserveOn(uiScheduler)
                .SortAndBind(Items, sortComparers)
                .Subscribe(),
            rows,
        ];
        return disposables;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCancellation.Cancel();
        _disposables.Dispose();
    }

    private void ApplyDirectoryChange(ISourceCache<SpecFileEntryViewModel, FilePathKey> cache, DirectoryChange change)
    {
        if (_disposed)
        {
            return;
        }

        switch (change.Kind)
        {
            case DirectoryChangeKind.Created:
            case DirectoryChangeKind.Changed:
                AddOrRemove(cache, change.Path);
                break;
            case DirectoryChangeKind.Deleted:
                cache.RemoveKey(GetPathCacheKey(change.Path));
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    cache.RemoveKey(GetPathCacheKey(oldPath));
                }

                AddOrRemove(cache, change.Path);
                break;
        }
    }

    private void AddOrRemove(ISourceCache<SpecFileEntryViewModel, FilePathKey> cache, string path)
    {
        var normalizedPath = NormalizedPath.FromFullyQualifiedPath(path);
        var model = _fileEntryRowReader.TryRead(normalizedPath, _scanCancellation.Token);
        if (model is null)
        {
            cache.RemoveKey(GetPathCacheKey(path));
            return;
        }

        cache.AddOrUpdate(model);
    }

    private static FilePathKey GetPathCacheKey(string path) => new(Path.GetFullPath(NormalizedPath.FromUserInput(path).DisplayPath));

    private IComparer<SpecFileEntryViewModel> CreateComparer(SortState sortState) =>
        new SpecFileEntryComparer(sortState.Column, sortState.Ascending, _displayStringCache);
}
