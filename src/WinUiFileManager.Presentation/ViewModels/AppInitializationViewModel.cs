using WinUiFileManager.Application.Settings;

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
    public partial bool InspectorVisible { get; private set; } = true;

    [ObservableProperty]
    public partial string LeftInitialPath { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RightInitialPath { get; private set; } = string.Empty;

    public async Task InitializeAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        InspectorVisible = settings.InspectorVisible;

        AvailableVolumes.Clear();
        var volumes = await _volumePolicyService.GetNtfsVolumesAsync(cancellationToken);
        foreach (var volume in volumes)
        {
            AvailableVolumes.Add(volume);
        }

        LeftInitialPath = ResolveInitialPath(settings.LastLeftPanePath);
        RightInitialPath = ResolveInitialPath(settings.LastRightPanePath);
    }

    private string ResolveInitialPath(NormalizedPath? savedPath)
    {
        if (savedPath is { } path)
        {
            return ResolveExistingFolderOrDrive(path.DisplayPath);
        }

        return GetFirstAvailableRoot();
    }

    private string ResolveExistingFolderOrDrive(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GetFirstAvailableRoot();
        }

        var current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return GetFirstAvailableRoot();
    }

    private string GetFirstAvailableRoot()
    {
        foreach (var volume in AvailableVolumes)
        {
            if (Directory.Exists(volume.RootPath.DisplayPath))
            {
                return volume.RootPath.DisplayPath;
            }
        }

        return Directory.GetLogicalDrives().FirstOrDefault(Directory.Exists)
            ?? Path.GetPathRoot(Environment.SystemDirectory)
            ?? @"C:\";
    }
}
