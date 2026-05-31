using System.Reactive.Concurrency;
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
/// filtered to this pane via <see cref="IdentityFilter.For{T}"/> (pane-scoped — see AGENTS.md §4). Registration is
/// idempotent (guarded by <see cref="_initialized"/>). <see cref="Dispose"/> unregisters and disposes the data source.
/// </para>
/// <para>
/// Ownership/lifetime: this view model owns the live <see cref="_dataSource"/>; the previous source is disposed
/// whenever a new one is applied, and the final one on <see cref="Dispose"/>. Navigation can arrive on a background
/// thread, so the new source's rows/path are published on <see cref="ISchedulerProvider.MainThread"/>.
/// </para>
/// </remarks>
public sealed partial class PanelFileEntryDataSourceViewModel : ObservableObject, IDisposable
{
    private readonly string _identity;
    private readonly Func<string, NormalizedPath, FileEntryTableDataSource> _dataSourceFactory;
    private readonly IMessenger _messenger;
    private readonly ISchedulerProvider _schedulers;
    private FileEntryTableDataSource? _dataSource;
    private bool _initialized;
    private bool _disposed;

    /// <param name="identity">Pane identity used to scope navigation messages to this pane.</param>
    /// <param name="dataSourceFactory">Builds a data source for a (identity, path) pair.</param>
    public PanelFileEntryDataSourceViewModel(
        string identity,
        IMessenger messenger,
        Func<string, NormalizedPath, FileEntryTableDataSource> dataSourceFactory,
        ISchedulerProvider schedulers)
    {
        _identity = identity;
        _messenger = messenger;
        _dataSourceFactory = dataSourceFactory;
        _schedulers = schedulers;
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
    public partial ObservableCollection<SpecFileEntryViewModel> Items { get; set; } = [];

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

        _messenger.Register(this, IdentityFilter.For<FileTableNavigateToPathMessage>(_identity, OnNavigateToPath));
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
    private void OnNavigateToPath(FileTableNavigateToPathMessage message)
    {
        var dataSource = _dataSourceFactory(_identity, message.Path);
        ApplyDataSource(dataSource);
    }

    /// <summary>
    /// Swaps in a new data source, disposing the previous one, and publishes its rows/path on the UI thread.
    /// </summary>
    /// <remarks>
    /// If disposal raced ahead of this call, the incoming <paramref name="dataSource"/> is disposed immediately to
    /// avoid leaking the just-created (background-watching) source. The rows/path assignment is marshalled to
    /// <see cref="ISchedulerProvider.MainThread"/> because navigation may run off the UI thread.
    /// </remarks>
    private void ApplyDataSource(FileEntryTableDataSource dataSource)
    {
        if (_disposed)
        {
            dataSource.Dispose();
            return;
        }

        _dataSource?.Dispose();
        _dataSource = dataSource;
        _schedulers.MainThread.Schedule(() =>
        {
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
