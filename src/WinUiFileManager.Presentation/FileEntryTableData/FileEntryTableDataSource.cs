using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// The reactive data pipeline behind one pane's file table: it performs the initial folder scan, keeps the
/// row set live by reacting to filesystem watcher changes, applies the requested sort, and exposes the
/// result as the observable <see cref="Items"/> collection bound to the table.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="FileEntryObservableRowStore"/> (the R3/ObservableCollections successor to the former
/// DynamicData <c>SourceCache</c> + <c>SortAndBind</c>): the store is the authoritative keyed, sorted row
/// set, keyed by normalised full path so a scan row and a later watcher update for the same file collapse
/// onto one entry. Each store mutation reports a <see cref="RowMutation"/> (or removed index) which is
/// mirrored onto <see cref="Items"/> with a single granular operation, so the bound table updates
/// incrementally instead of rebuilding - what keeps large folders responsive.
/// </para>
/// <para>
/// Threading: the folder enumeration runs on the background scheduler, but every store and
/// <see cref="Items"/> mutation is marshalled onto the UI scheduler (<c>ObserveOn</c>). The store is
/// single-writer, and here that single writer is the UI thread; the seed, watcher changes, and sort
/// requests are funnelled through one serialized, UI-thread subscription so they never race. (Moving the
/// writer to a dedicated background thread with a synchronized view is the planned next step.)
/// </para>
/// <para>
/// Lifetime/disposal: the pipeline subscription and the scan
/// <see cref="CancellationTokenSource"/> are owned by a single <see cref="CompositeDisposable"/>; the store
/// is disposed after the subscription is torn down (so nothing mutates it concurrently). This instance is
/// created and disposed by the owning pane view model - if that <see cref="Dispose"/> is not reached, the
/// watcher subscription leaks.
/// </para>
/// </remarks>
public sealed class FileEntryTableDataSource : IDisposable
{
    // Owns the pipeline subscription plus the scan CTS; disposed once in Dispose().
    private readonly CompositeDisposable _disposables;
    private readonly IFolderEntryScanner _folderEntryScanner;
    private readonly IFileEntryRowReader _fileEntryRowReader;
    private readonly CancellationTokenSource _scanCancellation = new();
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private readonly FileEntryObservableRowStore _store = new();
    private readonly NormalizedPath _folderPath;
    private readonly Identity _identity;

    // The order the store currently maintains. Established to the default at construction so the seed is
    // sorted before the first sort request arrives; thereafter mutated only on the UI thread.
    private SortState _sortState;
    private IComparer<SpecFileEntryViewModel> _comparer;
    private bool _disposed;

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
        _sortState = SortState.Default;
        _comparer = CreateComparer(_sortState);

        _disposables = Initialize(schedulers, directoryChangeStream, messenger);
        _disposables.Add(_scanCancellation);
    }

    /// <summary>The display path of the folder this source represents.</summary>
    public string CurrentPath { get; }

    /// <summary>The sorted, UI-bound row collection. Mutated only on the UI thread; bound to the table's
    /// <see cref="SpecFileEntryTableView.ItemsSource"/>. Kept in exact lockstep with the store's row list
    /// by mirroring each store mutation.</summary>
    public ObservableCollectionExtended<SpecFileEntryViewModel> Items { get; } = [];

    /// <summary>Builds the pipeline: an off-thread initial scan that seeds the store, the filesystem watcher
    /// that mutates it incrementally, and the pane-scoped sort-request stream - all merged into one
    /// UI-thread subscription so the single-writer store and the bound collection are touched on one thread.</summary>
    private CompositeDisposable Initialize(
        ISchedulerProvider schedulers,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        var uiScheduler = schedulers.MainThread;

        // Initial folder scan: enumerate off the UI thread, then publish a single seed (Reset) action.
        // Cancellation (navigation/disposal) ends the scan quietly rather than faulting the pipeline.
        var seed = Observable
            .Defer(() =>
            {
                try
                {
                    return Observable.Return(_folderEntryScanner.Scan(_folderPath, _scanCancellation.Token));
                }
                catch (OperationCanceledException)
                {
                    return Observable.Empty<IReadOnlyList<SpecFileEntryViewModel>>();
                }
            })
            .SubscribeOn(schedulers.Background)
            .Select(rows => new Action(() => ResetRows(rows)));

        // Filesystem watcher -> one incremental action per change. Subscribed (via Concat) only after the
        // seed completes, so a watcher update can never be applied before the initial rows exist.
        var changes = directoryChangeStream
            .Watch(_folderPath)
            .Select(change => new Action(() => ApplyDirectoryChange(change)));

        // Sort requests scoped to this pane -> re-sort action.
        var sorts = messenger
            .CreateObservable<FileTableSortRequestedMessage>()
            .Where(message => message.Identity == _identity)
            .Select(message => new Action(() => ApplySort(new SortState(message.Column, message.Ascending))));

        return
        [
            seed.Concat(changes)
                .Merge(sorts)
                .ObserveOn(uiScheduler)
                .Subscribe(static action => action()),
        ];
    }

    /// <summary>Cancels the in-flight scan and tears down the pipeline, then the store. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scanCancellation.Cancel();
        // Dispose the subscription first so no seed/change/sort action can run concurrently with the store
        // teardown below (the store is not thread-safe).
        _disposables.Dispose();
        _store.Dispose();
    }

    /// <summary>Seeds the store with the scan result under the active comparer and batch-loads the bound
    /// collection from the sorted rows.</summary>
    private void ResetRows(IReadOnlyList<SpecFileEntryViewModel> rows)
    {
        if (_disposed)
        {
            return;
        }

        _store.Reset(rows, _comparer);
        Items.Load(_store.Rows);
    }

    /// <summary>Adopts a new sort state, re-sorts the store, and batch-reloads the bound collection (a sort
    /// is a full reorder, so a single rebuild is the cheapest projection).</summary>
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
        Items.Load(_store.Rows);
    }

    /// <summary>Translates a watcher <see cref="DirectoryChange"/> into add/update/remove operations on the
    /// store and mirrors them onto <see cref="Items"/>. Guarded against running after disposal (the watcher
    /// may emit late).</summary>
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
                RemoveByKey(GetPathCacheKey(change.Path));
                break;
            case DirectoryChangeKind.Renamed:
                if (change.OldPath is { } oldPath)
                {
                    RemoveByKey(GetPathCacheKey(oldPath));
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
        var model = _fileEntryRowReader.TryRead(normalizedPath, _scanCancellation.Token);
        if (model is null)
        {
            RemoveByKey(GetPathCacheKey(path));
            return;
        }

        ApplyMutation(_store.AddOrUpdate(model), model);
    }

    /// <summary>Removes a row by key from the store and mirrors the removal onto <see cref="Items"/>.</summary>
    private void RemoveByKey(FilePathKey key)
    {
        var index = _store.Remove(key);
        if (index >= 0)
        {
            Items.RemoveAt(index);
        }
    }

    /// <summary>Applies one store <see cref="RowMutation"/> to <see cref="Items"/>, which is held in lockstep
    /// with the store's row list (so indices match exactly).</summary>
    private void ApplyMutation(RowMutation mutation, SpecFileEntryViewModel row)
    {
        switch (mutation.Kind)
        {
            case RowMutationKind.Inserted:
                Items.Insert(mutation.Index, row);
                break;
            case RowMutationKind.Replaced:
                Items[mutation.Index] = row;
                break;
            case RowMutationKind.Moved:
                // The row instance itself changed (a fresh re-read), so remove the old and insert the new
                // rather than Move (which would keep the stale instance).
                Items.RemoveAt(mutation.FromIndex);
                Items.Insert(mutation.Index, row);
                break;
        }
    }

    /// <summary>Computes the row key for a raw path string, matching the shape produced by
    /// <see cref="SpecFileEntryViewModel.GetKey"/> so removals line up with the seeded rows.</summary>
    private static FilePathKey GetPathCacheKey(string path) => new(Path.GetFullPath(NormalizedPath.FromUserInput(path).DisplayPath));

    private IComparer<SpecFileEntryViewModel> CreateComparer(SortState sortState) =>
        new SpecFileEntryComparer(sortState.Column, sortState.Ascending, _displayStringCache);
}
