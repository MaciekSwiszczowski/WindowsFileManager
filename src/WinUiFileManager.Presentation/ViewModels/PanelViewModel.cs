using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class PanelViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public PanelViewModel(
        string identity,
        IFolderEntryScanner folderEntryScanner,
        IFileEntryRowReader fileEntryRowReader,
        IDirectoryChangeStream directoryChangeStream,
        IMessenger messenger)
    {
        Identity = identity;
        FileEntries = new PanelFileEntryDataSourceViewModel(
            identity,
            folderEntryScanner,
            fileEntryRowReader,
            directoryChangeStream,
            messenger);
    }

    public string Identity { get; }

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
