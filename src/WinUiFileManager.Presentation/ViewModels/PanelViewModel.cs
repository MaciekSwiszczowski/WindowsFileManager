using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject, IDisposable
{
    public delegate PanelViewModel Factory(string identity);

    private readonly AppInitializationViewModel _initialization;
    private readonly IMessenger _messenger;
    private bool _initialNavigationRequested;
    private bool _disposed;

    public PanelViewModel(
        string identity,
        IMessenger messenger,
        AppInitializationViewModel initialization,
        PanelFileEntryDataSourceViewModel.Factory fileEntriesFactory)
    {
        Identity = identity;
        _messenger = messenger;
        _initialization = initialization;
        FileEntries = fileEntriesFactory(identity);
    }

    public string Identity { get; }

    public IMessenger Messenger => _messenger;

    public PanelFileEntryDataSourceViewModel FileEntries { get; }

    public ObservableCollection<VolumeInfo> AvailableVolumes => _initialization.AvailableVolumes;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    public void Initialize()
    {
        FileEntries.Initialize();
        _initialization.PropertyChanged += OnInitializationPropertyChanged;
        EnsureInitialNavigation();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initialization.PropertyChanged -= OnInitializationPropertyChanged;
        FileEntries.Dispose();
    }

    private void EnsureInitialNavigation()
    {
        if (_initialNavigationRequested)
        {
            return;
        }

        var initialPath = string.Equals(Identity, "Left", StringComparison.OrdinalIgnoreCase)
            ? _initialization.LeftInitialPath
            : _initialization.RightInitialPath;
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            return;
        }

        _initialNavigationRequested = true;
        _messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, new NormalizedPath(initialPath)));
        _messenger.Send(new FileTableColumnLayoutMessage(Identity, ColumnLayout.Default));
    }

    private void OnInitializationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppInitializationViewModel.LeftInitialPath) or nameof(AppInitializationViewModel.RightInitialPath))
        {
            EnsureInitialNavigation();
        }
    }
}
