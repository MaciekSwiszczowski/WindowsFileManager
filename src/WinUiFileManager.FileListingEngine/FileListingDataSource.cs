using ObservableCollections;
using R3;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.FileListingEngine.Messages;

namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// The reactive data pipeline behind one pane's file table: it performs the initial folder scan, keeps the
/// row set live by reacting to filesystem watcher changes, applies the requested sort, and exposes the
/// result as the bindable <see cref="Items"/> list bound to the table.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="FileListingRowStore"/> (R3 + ObservableCollections). The store is the authoritative keyed, sorted
/// row set, keyed by normalised full path so a scan row and a later watcher update for the same file collapse
/// onto one entry. <see cref="Items"/> is a thin <c>INotifyCollectionChanged</c> adapter over the store's
/// <see cref="FileListingRowStore.Rows"/> (<see cref="ObservableList{T}.ToNotifyCollectionChangedSlim()"/>):
/// there is no second copy of the rows and no hand-written mirror — each granular store mutation surfaces to
/// the table directly.
/// </para>
/// <para>
/// Pipeline: the folder enumeration runs on the thread pool; the watcher and pane-scoped sort requests are
/// merged after it (<c>Concat</c> so the seed always applies first) and the whole stream is marshalled onto
/// the UI thread (<c>ObserveOn</c>) before touching the store. The store is single-writer and that writer is
/// the UI thread, so the seed, watcher changes, and sort requests never race. (Moving the writer to a
/// dedicated background thread with a dispatcher-backed adapter is the planned next step.)
/// </para>
/// <para>
/// Lifetime/disposal: the scan <see cref="CancellationTokenSource"/> and the pipeline subscription are
/// cancelled/disposed first so nothing mutates the store concurrently, then the adapter and the store are
/// disposed. This instance is created and disposed by the owning pane view model — if that
/// <see cref="Dispose"/> is not reached, the watcher subscription leaks.
/// </para>
/// </remarks>
public sealed class FileListingDataSource : IDisposable
{
    private readonly IFolderListingScanner _folderListingScanner;
    private readonly IFileListingRowReader _fileListingRowReader;
    private readonly CancellationTokenSource _scanCancellation = new();
    private readonly IFileListingStringCache _displayStringCache;
    private readonly FileListingRowStore _store = new();
    private readonly NotifyCollectionChangedSynchronizedViewList<FileListingRow> _rows;
    private readonly IDisposable _subscription;
    private readonly NormalizedPath _folderPath;
    private readonly Identity _identity;

    // The order the store currently maintains. Established to the default at construction so the seed is
    // sorted before the first sort request arrives; thereafter mutated only on the UI thread.
    private SortState _sortState;
    private IComparer<FileListingRow> _comparer;
    private bool _disposed;

    public FileListingDataSource(
        string identity,
        NormalizedPath folderPath,
        SynchronizationContext uiSynchronizationContext,
        IFolderListingScanner folderListingScanner,
        IFileListingRowReader fileListingRowReader,
        IDirectoryChangeStream directoryChangeStream,
        IFileManagerMessenger messenger,
        IFileListingStringCache displayStringCache)
    {
        _identity = identity;
        _folderPath = folderPath;
        CurrentPath = folderPath.DisplayPath;
        _folderListingScanner = folderListingScanner;
        _fileListingRowReader = fileListingRowReader;
        _displayStringCache = displayStringCache;
        _sortState = SortState.Default;
        _comparer = CreateComparer(_sortState);

        // A slim adapter: bindable INotifyCollectionChanged over the store's rows with no extra copy. Store
        // writes happen on the UI thread, so the adapter needs no dispatcher (same-thread notifications).
        _rows = _store.Rows.ToNotifyCollectionChangedSlim();
        _subscription = Initialize(uiSynchronizationContext, directoryChangeStream, messenger);
    }

    /// <summary>The display path of the folder this source represents.</summary>
    public string CurrentPath { get; }

    /// <summary>The sorted, UI-bound row list — a thin adapter over the store's rows. Mutated only on the UI
    /// thread and consumed by the presentation table's item source.</summary>
    public NotifyCollectionChangedSynchronizedViewList<FileListingRow> Items => _rows;

    /// <summary>Builds the pipeline: an off-thread initial scan that seeds the store, the filesystem watcher
    /// that mutates it incrementally, and the pane-scoped sort-request stream — all merged into one UI-thread
    /// subscription so the single-writer store is only ever touched on one thread.</summary>
    private IDisposable Initialize(
        SynchronizationContext uiSynchronizationContext,
        IDirectoryChangeStream directoryChangeStream,
        IFileManagerMessenger messenger)
    {
        // Initial folder scan: enumerate on the thread pool, then publish a single seed command.
        // Cancellation (navigation/disposal) ends the scan quietly rather than faulting the pipeline.
        var seed = Observable
            .Defer(() =>
            {
                try
                {
                    return Observable.Return(_folderListingScanner.Scan(_folderPath, _scanCancellation.Token));
                }
                catch (OperationCanceledException)
                {
                    return Observable.Empty<IReadOnlyList<FileListingRow>>();
                }
            })
            .SubscribeOnThreadPool()
            .Select(static rows => DataSourceCommand.Seed(rows));

        // Filesystem watcher → one incremental action per change. Concat'd after the seed so a watcher update
        // can never be applied before the initial rows exist.
        var changes = directoryChangeStream
            .Watch(_folderPath)
            .Select(static change => DataSourceCommand.Change(change));

        // Sort requests scoped to this pane → re-sort action, filtered by pane identity with a static
        // (allocation-free) predicate.
        var sorts = messenger
            .CreateObservable<FileTableSortRequestedMessage>()
            .Where(_identity, static (message, identity) => message.Identity == identity)
            .Select(static message => DataSourceCommand.Sort(new SortState(message.Column, message.Ascending)));

        return seed.Concat(changes)
            .Merge(sorts)
            .ObserveOn(uiSynchronizationContext)
            .Subscribe(this, static (command, source) => source.ApplyCommand(command));
    }

    /// <summary>Cancels the in-flight scan, tears down the pipeline, then the adapter and store. Idempotent.
    /// Call on the UI thread: it disposes the UI-bound notify-adapter (<see cref="Items"/>) and the single-writer
    /// store. The owning pane view model disposes it on the UI thread (navigation swap or window teardown).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCancellation.Cancel();
        // Stop the pipeline first so no seed/change/sort action can run concurrently with the teardown below
        // (the store is not thread-safe). Then detach the adapter and clear the store.
        _subscription.Dispose();
        _scanCancellation.Dispose();
        _rows.Dispose();
        _store.Dispose();
    }

    /// <summary>Seeds the store with the scan result under the active comparer; the adapter surfaces the
    /// batched change to the table.</summary>
    private void ResetRows(IReadOnlyList<FileListingRow> rows)
    {
        if (_disposed)
        {
            return;
        }

        _store.Reset(rows, _comparer);
    }

    /// <summary>Adopts a new sort state and re-sorts the store; the adapter surfaces the reorder to the table.
    /// No-op when the sort is unchanged or the store is still empty (the seed will apply the new order).</summary>
    private void ApplySort(SortState sortState)
    {
        if (_disposed || sortState == _sortState)
        {
            return;
        }

        _sortState = sortState;
        _comparer = CreateComparer(sortState);
        if (_store.Rows.Count == 0)
        {
            return;
        }

        _store.Sort(_comparer);
    }

    /// <summary>Translates a watcher <see cref="DirectoryChange"/> into add/update/remove operations on the
    /// store. Guarded against running after disposal (the watcher may emit late).</summary>
    private void ApplyDirectoryChange(DirectoryChange change)
    {
        if (_disposed)
        {
            return;
        }

        switch (change.Kind)
        {
            case DirectoryChangeKind.Created:
            case DirectoryChangeKind.Changed:
                AddOrRemove(change.Path);
                break;
            case DirectoryChangeKind.Deleted:
                _store.Remove(GetPathCacheKey(change.Path));
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    _store.Remove(GetPathCacheKey(oldPath));
                }

                AddOrRemove(change.Path);
                break;
        }
    }

    /// <summary>Re-reads a single entry and upserts it; if it can no longer be read (e.g. it vanished between
    /// the change notification and the read) the corresponding row is removed instead.</summary>
    private void AddOrRemove(string path)
    {
        var normalizedPath = NormalizedPath.FromFullyQualifiedPath(path);
        var model = _fileListingRowReader.TryRead(normalizedPath, _scanCancellation.Token);
        if (model is null)
        {
            _store.Remove(GetPathCacheKey(path));
            return;
        }

        _store.AddOrUpdate(model);
    }

    /// <summary>Computes the row key for a raw path string, matching the shape produced by
    /// <see cref="FileListingRow.GetKey"/> so removals line up with the seeded rows.</summary>
    private static FileListingPathKey GetPathCacheKey(string path) => new(Path.GetFullPath(NormalizedPath.FromUserInput(path).DisplayPath));

    private IComparer<FileListingRow> CreateComparer(SortState sortState) =>
        new FileListingRowComparer(sortState.Column, sortState.Ascending, _displayStringCache);

    private void ApplyCommand(DataSourceCommand command)
    {
        switch (command.Kind)
        {
            case DataSourceCommandKind.Seed:
                ResetRows(command.Rows);
                break;
            case DataSourceCommandKind.Change:
                ApplyDirectoryChange(command.DirectoryChange);
                break;
            case DataSourceCommandKind.Sort:
                ApplySort(command.SortState);
                break;
            default:
                throw new InvalidOperationException($"Unknown data-source command: {command.Kind}.");
        }
    }

    private enum DataSourceCommandKind
    {
        Seed,
        Change,
        Sort,
    }

    private readonly struct DataSourceCommand
    {
        private readonly IReadOnlyList<FileListingRow>? _rows;
        private readonly DirectoryChange? _change;

        private DataSourceCommand(
            DataSourceCommandKind kind,
            IReadOnlyList<FileListingRow>? rows,
            DirectoryChange? change,
            SortState sortState)
        {
            Kind = kind;
            _rows = rows;
            _change = change;
            SortState = sortState;
        }

        public DataSourceCommandKind Kind { get; }

        public IReadOnlyList<FileListingRow> Rows =>
            _rows ?? throw new InvalidOperationException("Seed command requires rows.");

        public DirectoryChange DirectoryChange =>
            _change ?? throw new InvalidOperationException("Change command requires a directory change.");

        public SortState SortState { get; }

        public static DataSourceCommand Seed(IReadOnlyList<FileListingRow> rows) =>
            new(DataSourceCommandKind.Seed, rows, change: null, SortState.Default);

        public static DataSourceCommand Change(DirectoryChange change) =>
            new(DataSourceCommandKind.Change, rows: null, change, SortState.Default);

        public static DataSourceCommand Sort(SortState sortState) =>
            new(DataSourceCommandKind.Sort, rows: null, change: null, sortState);
    }
}
