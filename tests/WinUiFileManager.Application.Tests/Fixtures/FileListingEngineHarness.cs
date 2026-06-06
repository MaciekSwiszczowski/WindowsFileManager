using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.FileListingEngine.Messages;
using WinUiFileManager.Presentation.Messaging;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Application.Tests.Fixtures;

/// <summary>
/// Drives a real <see cref="FileListingDataSource"/> over a real folder for behavioral/end-to-end tests:
/// the genuine Windows scanner, reader, and <see cref="WindowsDirectoryChangeStream"/> are wired up, and the
/// data source is marshalled onto a <see cref="PumpingSynchronizationContext"/> (a deterministic single writer
/// thread) instead of a WinUI dispatcher. Tests create/modify real files, then poll <see cref="WaitForItems"/>
/// until the pipeline's <c>Items</c> reflect the change.
/// </summary>
/// <remarks>
/// The scanner/reader/watcher and the messenger are held for the harness lifetime (shared pipeline inputs); the
/// data source — the thing under test — is the only piece disposed eagerly, on the pump thread, by
/// <see cref="Dispose"/>. Items are always read back through the pump so reads never race the writer thread.
/// </remarks>
internal sealed class FileListingEngineHarness : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly PumpingSynchronizationContext _pump = new();
    private readonly StrongReferenceMessenger _innerMessenger = new();
    private readonly FileManagerMessenger _messenger;
    private readonly WindowsDirectoryChangeStream _watcher =
        new(NullLogger<WindowsDirectoryChangeStream>.Instance);

    private FileListingDataSource? _dataSource;

    public FileListingEngineHarness()
    {
        _messenger = new FileManagerMessenger(_innerMessenger);
    }

    /// <summary>The pane identity used for sort-request routing; defaults via <see cref="Start"/>.</summary>
    public string Identity { get; private set; } = "Test";

    /// <summary>The messenger the data source listens to for sort requests (use to push sort messages).</summary>
    public IFileManagerMessenger Messenger => _messenger;

    /// <summary>The live data source under test.</summary>
    public FileListingDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("Call Start(folderPath) before using the data source.");

    /// <summary>Builds and starts the data source over <paramref name="folderPath"/> using the real engine
    /// components. Call after seeding the folder with the files the initial scan should pick up.</summary>
    public void Start(string folderPath, string identity = "Test")
    {
        if (_dataSource is not null)
        {
            throw new InvalidOperationException("The harness data source has already been started.");
        }

        Identity = identity;
        var stringCache = FileEntryDisplayStringCache.Shared;
        var rowFactory = new FileListingRowFactory(static model => new FileListingRow(model), stringCache);
        _dataSource?.Dispose();
        _dataSource = new FileListingDataSource(
            identity,
            NormalizedPath.FromUserInput(folderPath),
            _pump,
            new WindowsFolderListingScanner(rowFactory),
            new WindowsFileListingRowReader(rowFactory),
            _watcher,
            _messenger,
            stringCache);
    }

    /// <summary>Sends a pane-scoped sort request (as the table's sorting behavior would), routed to this
    /// harness's data source by matching identity.</summary>
    public void RequestSort(SortColumn column, bool ascending) =>
        _messenger.Send(new FileTableSortRequestedMessage(Identity, column, ascending));

    /// <summary>Snapshots the current rows (read on the pump thread to avoid racing the writer). Reads via the
    /// <c>IList</c> indexer because the slim notify-adapter is a binding forwarder that does not support
    /// enumeration/<c>CopyTo</c> (WinUI reads it the same way).</summary>
    public IReadOnlyList<FileListingRow> SnapshotItems() => _pump.Invoke(ReadItemsViaIndexer);

    private IReadOnlyList<FileListingRow> ReadItemsViaIndexer()
    {
        // The slim notify-adapter is a binding forwarder: it throws NotSupportedException on enumeration/CopyTo,
        // so rows must be read one at a time through the IList indexer (exactly how WinUI materializes them). Do
        // NOT "simplify" this into AddRange/ToList/a collection expression — those enumerate and will throw.
        var items = DataSource.Items;
        var snapshot = new FileListingRow[items.Count];
        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index] = items[index];
        }

        return snapshot;
    }

    /// <summary>The names of the current real-entry rows (parent ".." row excluded).</summary>
    public IReadOnlyList<string> SnapshotEntryNames() =>
        SnapshotItems()
            .Where(static row => row.Model is not null)
            .Select(static row => row.Model!.Name)
            .ToList();

    /// <summary>Polls <c>Items</c> until <paramref name="predicate"/> holds, then returns that snapshot.
    /// Throws <see cref="TimeoutException"/> (with the last seen rows) if it never holds.</summary>
    public IReadOnlyList<FileListingRow> WaitForItems(
        Func<IReadOnlyList<FileListingRow>, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        while (true)
        {
            var snapshot = SnapshotItems();
            if (predicate(snapshot))
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                var seen = string.Join(", ", snapshot.Select(static row => row.Model?.Name ?? ".."));
                throw new TimeoutException($"Items did not satisfy the predicate within the timeout. Last seen: [{seen}].");
            }

            Thread.Sleep(20);
        }
    }

    public void Dispose()
    {
        // Dispose the data source on the pump thread (its single-writer thread), then stop the pump, then the
        // watcher. Ordering matters: the data source must stop before the pump drains.
        if (_dataSource is { } dataSource)
        {
            _pump.Send(_ => dataSource.Dispose(), null);
            _dataSource?.Dispose();
            _dataSource = null;
        }

        _pump.Dispose();
        _watcher.Dispose();
    }
}
