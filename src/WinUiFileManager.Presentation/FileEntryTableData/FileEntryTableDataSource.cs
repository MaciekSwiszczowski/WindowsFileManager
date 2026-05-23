using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private const string ParentEntryKey = "\0..";

    private readonly CompositeDisposable _disposables;
    private readonly IFileEntryDataReader _fileEntryDataReader;
    private readonly IMessenger _messenger;
    private readonly CancellationTokenSource _scanCancellation = new();
    private SortState _sortState = SortState.Default;
    private bool _disposed;

    public FileEntryTableDataSource(
        string identity,
        NormalizedPath folderPath,
        IScheduler uiScheduler,
        IFileEntryDataReader fileEntryDataReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentNullException.ThrowIfNull(uiScheduler);
        ArgumentNullException.ThrowIfNull(fileEntryDataReader);
        ArgumentNullException.ThrowIfNull(directoryChangeStream);
        ArgumentNullException.ThrowIfNull(messenger);

        Identity = identity;
        FolderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        _fileEntryDataReader = fileEntryDataReader;
        _messenger = messenger;

        _messenger.Register<FileTableSortRequestedMessage>(this, OnSortRequested);
        _disposables =
        [
            Disposable.Create(this, static vm => vm._messenger.Unregister<FileTableSortRequestedMessage>(vm)),
            _scanCancellation,
            CreateRows(directoryChangeStream)
                .ObserveOn(uiScheduler)
                .Bind(Items)
                .Subscribe(),
        ];
    }

    private string Identity { get; }

    private NormalizedPath FolderPath { get; }

    public string CurrentPath { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];

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

    private IObservable<SpecFileEntryViewModel> ScanCurrentFolder() =>
        Observable.Create<SpecFileEntryViewModel>(observer =>
        {
            var subject = new Subject<SpecFileEntryViewModel>();
            var subjectSubscription = subject.Subscribe(observer);

            if (Directory.GetParent(CurrentPath) is not null)
            {
                subject.OnNext(SpecFileEntryViewModel.CreateParentEntry());
            }

            var entriesSubscription = _fileEntryDataReader.GetEntries(FolderPath, _scanCancellation.Token)
                .ToObservable()
                .Subscribe(subject);

            return Disposable.Create(this, _ =>
            {
                entriesSubscription.Dispose();
                subjectSubscription.Dispose();
                subject.Dispose();
            });
        });

    private IObservable<IChangeSet<SpecFileEntryViewModel, string>> CreateRows(IDirectoryChangeStream directoryChangeStream)
    {
        Func<ISourceCache<SpecFileEntryViewModel, string>, IDisposable> subscribe = cache => ScanCurrentFolder()
            .Do(cache.AddOrUpdate)
            .Select(static _ => Unit.Default)
            .Merge(directoryChangeStream
                .Watch(FolderPath)
                .Do(change => ApplyDirectoryChange(cache, change))
                .Select(static _ => Unit.Default))
            .Subscribe();

        return ObservableChangeSet.Create(subscribe, GetKey);
    }

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
                cache.RemoveKey(GetChangePathKey(change.Path));
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    cache.RemoveKey(GetChangePathKey(oldPath));
                }

                AddOrRemove(cache, change.Path);
                break;
        }
    }

    private void AddOrRemove(ISourceCache<SpecFileEntryViewModel, string> cache, string path)
    {
        var normalizedPath = NormalizeChangePath(path);
        var model = TryGetEntry(normalizedPath);
        if (model is null)
        {
            cache.RemoveKey(normalizedPath.Value);
            return;
        }

        cache.AddOrUpdate(model);
    }

    private SpecFileEntryViewModel? TryGetEntry(NormalizedPath path)
    {
        try
        {
            return _fileEntryDataReader.GetEntry(path, _scanCancellation.Token);
        }
        catch
        {
            return null;
        }
    }

    private void OnSortRequested(object recipient, FileTableSortRequestedMessage message)
    {
        if (message.Identity != Identity)
        {
            return;
        }

        _sortState = new SortState(message.Column, message.Ascending);
    }

    private static string GetKey(SpecFileEntryViewModel item) =>
        item.Model?.FullPath.Value ?? ParentEntryKey;

    private static string GetChangePathKey(string path) => NormalizeChangePath(path).Value;

    private static NormalizedPath NormalizeChangePath(string path) =>
        NormalizedPath.FromFullyQualifiedPath(path);
}
