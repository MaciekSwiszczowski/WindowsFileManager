using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject, IDisposable
{
    public delegate PanelViewModel Factory(string identity);

    private bool _disposed;

    public PanelViewModel(
        string identity,
        IMessenger messenger,
        AppInitializationViewModel initialization,
        PanelFileEntryDataSourceViewModel.Factory fileEntriesFactory)
    {
        Identity = identity;
        Messenger = messenger;
        Initialization = initialization;
        FileEntries = fileEntriesFactory(identity);
    }

    public string Identity { get; }

    public IMessenger Messenger { get; }

    public AppInitializationViewModel Initialization { get; }

    public PanelFileEntryDataSourceViewModel FileEntries { get; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

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
