using System.Reactive.Concurrency;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Panels;

public sealed partial class PanelFileEntryDataSourceViewModel : ObservableObject, IDisposable
{
    private readonly string _identity;
    private readonly Func<string, NormalizedPath, FileEntryTableDataSource> _dataSourceFactory;
    private readonly IMessenger _messenger;
    private readonly ISchedulerProvider _schedulers;
    private FileEntryTableDataSource? _dataSource;
    private bool _initialized;
    private bool _disposed;

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

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditablePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PathValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<SpecFileEntryViewModel> Items { get; set; } = [];

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

    private void OnNavigateToPath(FileTableNavigateToPathMessage message)
    {
        var dataSource = _dataSourceFactory(_identity, message.Path);
        ApplyDataSource(dataSource);
    }

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

    public bool HasPathValidationError => !string.IsNullOrWhiteSpace(PathValidationMessage);

    partial void OnCurrentPathChanged(string value)
    {
        EditablePath = value;
        PathValidationMessage = string.Empty;
        OnPropertyChanged(nameof(HasPathValidationError));
    }

    partial void OnPathValidationMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasPathValidationError));
}
