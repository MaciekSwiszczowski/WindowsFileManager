using System.Reactive.Concurrency;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Panels;

public sealed partial class PanelFileEntryDataSourceViewModel : ObservableObject, IDisposable
{
    public delegate PanelFileEntryDataSourceViewModel Factory(string identity);

    private readonly string _identity;
    private readonly IFolderEntryScanner _folderEntryScanner;
    private readonly IFileEntryRowReader _fileEntryRowReader;
    private readonly IDirectoryChangeStream _directoryChangeStream;
    private readonly IMessenger _messenger;
    private FileEntryTableDataSource? _dataSource;
    private IScheduler? _uiScheduler;
    private bool _attached;
    private bool _disposed;

    public PanelFileEntryDataSourceViewModel(
        string identity,
        IFolderEntryScanner folderEntryScanner,
        IFileEntryRowReader fileEntryRowReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        _identity = identity;
        _folderEntryScanner = folderEntryScanner;
        _fileEntryRowReader = fileEntryRowReader;
        _directoryChangeStream = directoryChangeStream;
        _messenger = messenger;
    }

    [ObservableProperty]
    public partial string CurrentPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditablePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PathValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<SpecFileEntryViewModel> Items { get; set; } = [];

    public void Attach(IScheduler uiScheduler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(uiScheduler);

        if (_attached)
        {
            return;
        }

        _uiScheduler = uiScheduler;
        _messenger.Register(this, MessageIdentity.Filter<FileTableNavigateToPathMessage>(_identity, OnNavigateToPath));
        _attached = true;
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _messenger.UnregisterAll(this);
        _dataSource?.Dispose();
        _dataSource = null;
        _uiScheduler = null;
        Items = [];
        CurrentPath = string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Detach();
    }

    private void OnNavigateToPath(FileTableNavigateToPathMessage message) => ReplaceDataSource(message.Path);

    private void ReplaceDataSource(NormalizedPath folderPath)
    {
        if (_uiScheduler is null)
        {
            return;
        }

        _dataSource?.Dispose();
        _dataSource = new FileEntryTableDataSource(
            _identity,
            folderPath,
            _uiScheduler,
            _folderEntryScanner,
            _fileEntryRowReader,
            _directoryChangeStream,
            _messenger);

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
