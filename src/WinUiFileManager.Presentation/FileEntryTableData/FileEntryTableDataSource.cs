using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

internal sealed class FileEntryTableDataSource : IDisposable
{
    private readonly CompositeDisposable _disposables;
    private readonly IFileEntryDataReader _fileEntryDataReader;
    private readonly IMessenger _messenger;
    private readonly CancellationTokenSource _scanCancellation = new();
    private SortState _sortState = SortState.Default;
    private bool _disposed;
    private readonly string _identity;
    private readonly NormalizedPath _folderPath;

    public FileEntryTableDataSource(
        string identity,
        NormalizedPath folderPath,
        IScheduler uiScheduler,
        IFileEntryDataReader fileEntryDataReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        _identity = identity;
        _folderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        _fileEntryDataReader = fileEntryDataReader;
        _messenger = messenger;

        _messenger.Register<FileTableSortRequestedMessage>(this, OnSortRequested);

        _disposables = Initialize(uiScheduler, directoryChangeStream);
        _disposables.Add(Disposable.Create(this, static vm => vm._messenger.Unregister<FileTableSortRequestedMessage>(vm)));
        _disposables.Add(_scanCancellation);
    }

    public string CurrentPath { get; }

    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];


    private CompositeDisposable Initialize(IScheduler uiScheduler, IDirectoryChangeStream directoryChangeStream)
    {
        var rows = new SourceCache<SpecFileEntryViewModel, string>(static item => item.GetKey());
        rows.AddOrUpdate(ScanCurrentFolder());

        CompositeDisposable disposables =
        [
            directoryChangeStream
                .Watch(_folderPath)
                .Subscribe(change => ApplyDirectoryChange(rows, change)),
            rows.Connect()
                .ObserveOn(uiScheduler)
                .Bind(Items)
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

    private IReadOnlyList<SpecFileEntryViewModel> ScanCurrentFolder() =>
        _fileEntryDataReader.GetEntries(_folderPath, _scanCancellation.Token);

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
                cache.RemoveKey(NormalizedPath.FromFullyQualifiedPath(change.Path).Value);
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    cache.RemoveKey(NormalizedPath.FromFullyQualifiedPath(oldPath).Value);
                }

                AddOrRemove(cache, change.Path);
                break;
        }
    }

    private void AddOrRemove(ISourceCache<SpecFileEntryViewModel, string> cache, string path)
    {
        var normalizedPath = NormalizedPath.FromFullyQualifiedPath(path);
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
        if (message.Identity != _identity)
        {
            return;
        }

        _sortState = new SortState(message.Column, message.Ascending);
    }
}
