namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class AppInitializationViewModel : ObservableObject
{
    private readonly INtfsVolumePolicyService _volumePolicyService;
    private bool _initialized;

    public AppInitializationViewModel(INtfsVolumePolicyService volumePolicyService)
    {
        _volumePolicyService = volumePolicyService;
    }

    public ObservableCollection<VolumeInfo> AvailableVolumes { get; } = [];

    [ObservableProperty]
    public partial string LeftInitialPath { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RightInitialPath { get; private set; } = string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        AvailableVolumes.Clear();
        var volumes = await _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);
        foreach (var volume in volumes)
        {
            AvailableVolumes.Add(volume);
        }

        LeftInitialPath = ResolveInitialPath(
            @"C:\FileEntryTableTest\Left",
            Environment.SpecialFolder.UserProfile);
        RightInitialPath = ResolveInitialPath(
            @"C:\FileEntryTableTest\Right",
            Environment.SpecialFolder.DesktopDirectory);
    }

    private static string ResolveInitialPath(
        string preferredPath,
        Environment.SpecialFolder fallbackFolder)
    {
        if (Directory.Exists(preferredPath))
        {
            return preferredPath;
        }

        var fallbackPath = Environment.GetFolderPath(fallbackFolder);
        if (Directory.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
        {
            return userProfile;
        }

        return Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    }
}
