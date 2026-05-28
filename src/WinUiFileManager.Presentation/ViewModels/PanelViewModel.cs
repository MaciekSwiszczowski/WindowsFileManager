using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject, IDisposable
{
    public delegate PanelViewModel Factory(string identity);

    private readonly AppInitializationViewModel _initialization;
    private readonly IMessenger _messenger;
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
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FileEntries.Dispose();
    }
}
