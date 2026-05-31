using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// The reactive data pipeline behind one pane's file table: it performs the initial folder scan,
/// keeps the row set live by reacting to filesystem watcher changes, applies the requested sort, and
/// exposes the result as an observable <see cref="Items"/> collection bound to the table.
/// </summary>
/// <remarks>
/// <para>
/// Built on DynamicData: a <see cref="SourceCache{TObject,TKey}"/> keyed by <see cref="FilePathKey"/> is
/// the authoritative row set; <c>Connect().SortAndBind(Items, ...)</c> projects it (sorted) onto the
/// UI-bound <see cref="ObservableCollectionExtended{T}"/>. The cache key is the normalised full path so
/// a scan row and a later watcher update for the same file map to the same entry.
/// </para>
/// <para>
/// Lifetime/disposal (AGENTS.md §5): every subscription, the source cache, and the scan
/// <see cref="CancellationTokenSource"/> are owned by a single <see cref="CompositeDisposable"/> and
/// torn down in <see cref="Dispose"/>. This instance is created and disposed by the owning pane view
/// model — if that <see cref="Dispose"/> is not reached, the watcher subscription leaks.
/// </para>
/// <para>
/// Threading (AGENTS.md §6): cache connection and sort results are marshalled onto the UI scheduler
/// (<c>ObserveOn(uiScheduler)</c>) before touching <see cref="Items"/>, because the bound collection
/// must only be mutated on the UI thread.
/// </para>
/// </remarks>
public sealed class FileEntryTableDataSource : IDisposable
{
    // Owns the whole Rx/DynamicData pipeline plus the scan CTS; disposed once in Dispose().
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

    /// <summary>The display path of the folder this source represents.</summary>
    public string CurrentPath { get; }

    /// <summary>The sorted, UI-bound row collection. Mutated only on the UI thread by the DynamicData
    /// bind. Bound to the table's <see cref="SpecFileEntryTableView.ItemsSource"/>.</summary>
    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];


    /// <summary>Builds the pipeline: seeds the source cache from the initial scan, derives the sort
    /// comparer stream from incoming <see cref="FileTableSortRequestedMessage"/> (filtered to this
    /// pane), wires the watcher and the sorted bind, and returns the composite owning them all.</summary>
    private CompositeDisposable Initialize(
        IScheduler uiScheduler,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        var rows = new SourceCache<SpecFileEntryViewModel, FilePathKey>(static item => item.GetKey());
        // Seed synchronously with the initial folder scan; the watcher keeps it current afterwards.
        rows.AddOrUpdate(_folderEntryScanner.Scan(_folderPath, _scanCancellation.Token));

        // Sort requests for this pane become a stream of comparers; StartWith seeds the default order
        // so the table is sorted before the user touches a header.
        var sortComparers = messenger
            .CreateObservable<FileTableSortRequestedMessage>()
            .Where(message => message.Identity == _identity)
            .Select(message => CreateComparer(new SortState(message.Column, message.Ascending)))
            .StartWith(CreateComparer(SortState.Default))
            .ObserveOn(uiScheduler);

        CompositeDisposable disposables =
        [
            // Filesystem watcher → mutate the source cache. Applied off the bind; results flow through
            // Connect() below which marshals to the UI thread.
            directoryChangeStream
                .Watch(_folderPath)
                .Subscribe(change => ApplyDirectoryChange(rows, change)),
            // Project the cache (sorted) onto the UI-bound Items collection on the UI scheduler.
            rows.Connect()
                .ObserveOn(uiScheduler)
                .SortAndBind(Items, sortComparers)
                .Subscribe(),
            // The cache itself is disposable and must be released with the rest.
            rows,
        ];
        return disposables;
    }

    /// <summary>Cancels the in-flight scan and tears down the entire pipeline. Idempotent.</summary>
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

    /// <summary>Translates a watcher <see cref="DirectoryChange"/> into add/update/remove operations on
    /// the source cache. Guarded against running after disposal (the watcher may emit late).</summary>
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

    /// <summary>Re-reads a single entry and upserts it; if it can no longer be read (e.g. it vanished
    /// between the change notification and the read) the corresponding cache key is removed instead.</summary>
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

    /// <summary>Computes the cache key for a raw path string, matching the key shape produced by
    /// <see cref="SpecFileEntryViewModel.GetKey"/> so removals line up with the seeded rows.</summary>
    private static FilePathKey GetPathCacheKey(string path) => new(Path.GetFullPath(NormalizedPath.FromUserInput(path).DisplayPath));

    private IComparer<SpecFileEntryViewModel> CreateComparer(SortState sortState) =>
        new SpecFileEntryComparer(sortState.Column, sortState.Ascending, _displayStringCache);
}
