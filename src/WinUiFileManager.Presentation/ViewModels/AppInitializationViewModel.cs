namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Holds one-time startup state shared across the shell: the list of available NTFS volumes and the initial
/// inspector visibility derived from settings. Both panes reference the same <see cref="AvailableVolumes"/>
/// instance, so it is populated exactly once.
/// </summary>
/// <remarks>Owned by <see cref="MainShellViewModel"/>; holds no messenger registrations or disposable resources.</remarks>
public sealed partial class AppInitializationViewModel : ObservableObject
{
    private bool _initialized;

    /// <summary>Volumes shown in the drive pickers; populated once during <see cref="Initialize"/>.</summary>
    public ObservableCollection<VolumeInfo> AvailableVolumes { get; } = [];

    /// <summary>Initial inspector visibility from persisted settings (private setter — set only during init).</summary>
    [ObservableProperty]
    public partial bool InspectorVisible { get; private set; } = true;

    /// <summary>
    /// Applies startup settings and the discovered volume list. Idempotent: guarded by <see cref="_initialized"/>
    /// so repeated startup messages do not clear/repopulate the shared volume collection.
    /// </summary>
    public void Initialize(AppSettings settings, IReadOnlyList<VolumeInfo> volumes)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        InspectorVisible = settings.InspectorVisible;

        AvailableVolumes.Clear();
        foreach (var volume in volumes)
        {
            AvailableVolumes.Add(volume);
        }
    }
}
