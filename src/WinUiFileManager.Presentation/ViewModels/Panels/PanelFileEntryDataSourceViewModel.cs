using ObservableCollections;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Panels;

/// <summary>
/// Per-pane view model that owns the current <see cref="FileEntryTableDataSource"/> and exposes its rows
/// (<see cref="Items"/>) and path state to the table view. Responds to pane-scoped navigation requests by building
/// a fresh data source for the target directory and swapping it in.
/// </summary>
/// <remarks>
/// <para>
/// Messaging: <see cref="Initialize"/> registers a single recipient for <see cref="FileTableNavigateToPathMessage"/>,
/// filtered to this pane via the messenger wrapper's identity-aware registration (pane-scoped — see AGENTS.md §4).
/// Registration is idempotent (guarded by <see cref="_initialized"/>). <see cref="Dispose"/> unregisters and
/// disposes the data source.
/// </para>
/// <para>
/// Ownership/lifetime: this view model owns the live <see cref="_dataSource"/>; the previous source is disposed
/// whenever a new one is applied, and the final one on <see cref="Dispose"/>. Navigation can arrive on a background
/// thread, so the new source's rows/path are published on the UI thread via <see cref="IUiThreadDispatcher"/>.
/// </para>
/// </remarks>
public sealed partial class PanelFileEntryDataSourceViewModel : ObservableObject, IDisposable
{
    private readonly string _identity;
    private readonly Func<string, NormalizedPath, FileEntryTableDataSource> _dataSourceFactory;
    private readonly IFileManagerMessenger _messenger;
    private readonly IUiThreadDispatcher _uiDispatcher;
    private FileEntryTableDataSource? _dataSource;
    private bool _initialized;
    private bool _disposed;

    /// <param name="identity">Pane identity used to scope navigation messages to this pane.</param>
    /// <param name="dataSourceFactory">Builds a data source for a (identity, path) pair.</param>
    public PanelFileEntryDataSourceViewModel(
        string identity,
        IFileManagerMessenger messenger,
        Func<string, NormalizedPath, FileEntryTableDataSource> dataSourceFactory,
        IUiThreadDispatcher uiDispatcher)
    {
        _identity = identity;
        _messenger = messenger;
        _dataSourceFactory = dataSourceFactory;
        _uiDispatcher = uiDispatcher;
    }

    /// <summary>The committed current directory path of this pane (display form).</summary>
    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    /// <summary>The path text the user is editing in the address box; reset to <see cref="CurrentPath"/> on navigation.</summary>
    [ObservableProperty]
    public partial string EditablePath { get; set; } = string.Empty;

    /// <summary>Validation message for a rejected path entry; empty when the path is valid.</summary>
    [ObservableProperty]
    public partial string PathValidationMessage { get; set; } = string.Empty;

    /// <summary>
    /// The bound row collection for the table. Replaced wholesale when a new data source is applied; each row is a
    /// lean <see cref="SpecFileEntryViewModel"/> (see its leanness invariant) to keep large folders cheap.
    /// </summary>
    [ObservableProperty]
    public partial NotifyCollectionChangedSynchronizedViewList<SpecFileEntryViewModel>? Items { get; set; }

    /// <summary>
    /// Registers the pane-scoped navigation recipient. Idempotent (guarded by <see cref="_initialized"/>) per
    /// AGENTS.md §4 so it cannot double-register and double-handle navigations.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if called after disposal.</exception>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_initialized)
        {
            return;
        }

        _messenger.Register<FileTableNavigateToPathMessage>(this, _identity, OnNavigateToPath);
        _initialized = true;
    }

    /// <summary>Unregisters the recipient and disposes the live data source. Idempotent via <see cref="_disposed"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
        _dataSource?.Dispose();
        _dataSource = null;
    }

    /// <summary>Handles a pane-scoped navigate request by building and applying a data source for the target path.</summary>
    private void OnNavigateToPath(FileTableNavigateToPathMessage message) => ApplyDataSource(message.Path);

    /// <summary>
    /// Builds a data source for <paramref name="path"/> and swaps it in, disposing the previous one, publishing
    /// its rows/path on the UI thread.
    /// </summary>
    /// <remarks>
    /// The new source is created here (this view model owns its lifetime). The whole swap — dispose the previous
    /// source, adopt the new one, rebind rows/path — runs in one callback on the UI thread (navigation may arrive
    /// off it). Keeping it a single synchronous callback matters now that <see cref="FileEntryTableDataSource.Items"/>
    /// is a disposable adapter bound to the table: the table never observes the old adapter after disposal because
    /// the rebind happens in the same callback. If disposal races ahead, the new source is disposed instead of leaked.
    /// </remarks>
    private void ApplyDataSource(NormalizedPath path)
    {
        if (_disposed)
        {
            return;
        }

        var dataSource = _dataSourceFactory(_identity, path);
        _uiDispatcher.Post(() =>
        {
            if (_disposed)
            {
                dataSource.Dispose();
                return;
            }

            _dataSource?.Dispose();
            _dataSource = dataSource;
            Items = dataSource.Items;
            CurrentPath = dataSource.CurrentPath;
        });
    }

    /// <summary>True when <see cref="PathValidationMessage"/> is non-empty; drives error styling on the address box.</summary>
    public bool HasPathValidationError => !string.IsNullOrWhiteSpace(PathValidationMessage);

    /// <summary>When the committed path changes, reset the editable text and clear any validation error.</summary>
    partial void OnCurrentPathChanged(string value)
    {
        EditablePath = value;
        PathValidationMessage = string.Empty;
        OnPropertyChanged(nameof(HasPathValidationError));
    }

    /// <summary>Re-raises <see cref="HasPathValidationError"/> when the validation message changes.</summary>
    partial void OnPathValidationMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasPathValidationError));
}
