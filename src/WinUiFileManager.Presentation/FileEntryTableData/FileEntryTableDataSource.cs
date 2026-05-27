using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private readonly CompositeDisposable _disposables;
    private readonly IFolderEntryScanner _folderEntryScanner;
    private readonly IFileEntryRowReader _fileEntryRowReader;
    private readonly IMessenger _messenger;
    private readonly CancellationTokenSource _scanCancellation = new();
    private readonly BehaviorSubject<IComparer<SpecFileEntryViewModel>> _sortComparer;
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private bool _disposed;
    private readonly NormalizedPath _folderPath;

    public FileEntryTableDataSource(
        string identity,
        NormalizedPath folderPath,
        IScheduler uiScheduler,
        IFolderEntryScanner folderEntryScanner,
        IFileEntryRowReader fileEntryRowReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger,
        FileEntryDisplayStringCache displayStringCache)
    {
        _folderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        _folderEntryScanner = folderEntryScanner;
        _fileEntryRowReader = fileEntryRowReader;
        _messenger = messenger;
        _displayStringCache = displayStringCache;
        _sortComparer = new BehaviorSubject<IComparer<SpecFileEntryViewModel>>(CreateComparer(SortState.Default));

        _messenger.Register(this, MessageIdentity.Filter<FileTableSortRequestedMessage>(identity, OnSortRequested));

        _disposables = Initialize(uiScheduler, directoryChangeStream);
        _disposables.Add(Disposable.Create(this, static vm => vm._messenger.Unregister<FileTableSortRequestedMessage>(vm)));
        _disposables.Add(_scanCancellation);
    }

    public string CurrentPath { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];


    private CompositeDisposable Initialize(IScheduler uiScheduler, IDirectoryChangeStream directoryChangeStream)
    {
        var rows = new SourceCache<SpecFileEntryViewModel, string>(GetRowCacheKey);
        rows.AddOrUpdate(ScanCurrentFolder());

        CompositeDisposable disposables =
        [
            directoryChangeStream
                .Watch(_folderPath)
                .Subscribe(change => ApplyDirectoryChange(rows, change)),
            rows.Connect()
                .ObserveOn(uiScheduler)
                .SortAndBind(Items, _sortComparer.ObserveOn(uiScheduler))
                .Subscribe(),
            rows,
            _sortComparer,
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

    private IReadOnlyList<SpecFileEntryViewModel> ScanCurrentFolder() =>
        _folderEntryScanner.Scan(_folderPath, _scanCancellation.Token);

    private void ApplyDirectoryChange(ISourceCache<SpecFileEntryViewModel, string> cache, DirectoryChange change)
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

    private void AddOrRemove(ISourceCache<SpecFileEntryViewModel, string> cache, string path)
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

    private static string GetRowCacheKey(SpecFileEntryViewModel item) => GetPathCacheKey(item.GetKey());

    private static string GetPathCacheKey(string path) => NormalizedPath.FromUserInput(path).Value.ToUpperInvariant();

    private void OnSortRequested(FileTableSortRequestedMessage message)
    {
        _sortComparer.OnNext(CreateComparer(new SortState(message.Column, message.Ascending)));
    }

    private IComparer<SpecFileEntryViewModel> CreateComparer(SortState sortState) =>
        new SpecFileEntryComparer(sortState.Column, sortState.Ascending, _displayStringCache);
}
