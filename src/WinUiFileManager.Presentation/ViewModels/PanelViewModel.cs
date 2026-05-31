using WinUiFileManager.Presentation.ViewModels.Panels;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// View model for a single file-manager pane (left or right). Identified by <see cref="Identity"/>, it owns the
/// pane's <see cref="PanelFileEntryDataSourceViewModel"/> and exposes pane-level UI state (active flag, selected
/// count, the shared volume list).
/// </summary>
/// <remarks>
/// Created by <see cref="PanelsViewModel"/> via a factory keyed on identity. Disposal is cascaded from the parent
/// and forwards to <see cref="FileEntries"/>; this type holds no messenger registrations of its own.
/// </remarks>
public sealed partial class PanelViewModel : ObservableObject, IDisposable
{
    private readonly AppInitializationViewModel _initialization;
    private readonly IMessenger _messenger;
    private bool _disposed;

    /// <param name="identity">Pane identity constant (<c>"Left"</c>/<c>"Right"</c>); see AGENTS.md §4.</param>
    /// <param name="fileEntriesFactory">Factory that builds the pane's data-source view model for this identity.</param>
    public PanelViewModel(
        string identity,
        IMessenger messenger,
        AppInitializationViewModel initialization,
        Func<string, PanelFileEntryDataSourceViewModel> fileEntriesFactory)
    {
        Identity = identity;
        _messenger = messenger;
        _initialization = initialization;
        FileEntries = fileEntriesFactory(identity);
    }

    /// <summary>Stable pane identity used to scope pane-targeted messages.</summary>
    public string Identity { get; }

    /// <summary>App-wide messenger, exposed for behaviors/bindings bound to this pane.</summary>
    public IMessenger Messenger => _messenger;

    /// <summary>The pane's file listing / navigation data source.</summary>
    public PanelFileEntryDataSourceViewModel FileEntries { get; }

    /// <summary>Volumes available for the pane's drive picker; shared instance from initialization state.</summary>
    public ObservableCollection<VolumeInfo> AvailableVolumes => _initialization.AvailableVolumes;

    /// <summary>Whether this pane is the active one (drives focus styling).</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Number of selected rows in this pane (mirrored from selection messages).</summary>
    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    /// <summary>Initializes the underlying data source (idempotent at the data-source level).</summary>
    public void Initialize()
    {
        FileEntries.Initialize();
    }

    /// <summary>Disposes the owned data source. Idempotent via <see cref="_disposed"/>.</summary>
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
