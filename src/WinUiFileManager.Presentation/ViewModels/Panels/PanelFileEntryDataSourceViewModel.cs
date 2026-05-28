using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Panels;

public sealed partial class PanelFileEntryDataSourceViewModel : ObservableObject, IDisposable
{
    public delegate PanelFileEntryDataSourceViewModel Factory(string identity);

    private readonly string _identity;
    private readonly FileEntryTableDataSource.Factory _dataSourceFactory;
    private readonly IMessenger _messenger;
    private FileEntryTableDataSource? _dataSource;
    private bool _initialized;
    private bool _disposed;

    public PanelFileEntryDataSourceViewModel(
        string identity,
        IMessenger messenger,
        FileEntryTableDataSource.Factory dataSourceFactory)
    {
        _identity = identity;
        _messenger = messenger;
        _dataSourceFactory = dataSourceFactory;
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

    private void OnNavigateToPath(FileTableNavigateToPathMessage message) => ReplaceDataSource(message.Path);

    private void ReplaceDataSource(NormalizedPath folderPath)
    {
        _dataSource?.Dispose();
        _dataSource = _dataSourceFactory(_identity, folderPath);

        Items = _dataSource.Items;
        CurrentPath = _dataSource.CurrentPath;
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
