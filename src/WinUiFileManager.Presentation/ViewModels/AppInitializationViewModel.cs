namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class AppInitializationViewModel : ObservableObject
{
    private bool _initialized;

    public ObservableCollection<VolumeInfo> AvailableVolumes { get; } = [];

    [ObservableProperty]
    public partial bool InspectorVisible { get; private set; } = true;

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
