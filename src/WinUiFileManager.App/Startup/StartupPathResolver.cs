namespace WinUiFileManager.App.Startup;

using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;

public sealed class StartupPathResolver
{
    public StartupPanelPaths Resolve(AppSettings settings, IReadOnlyList<VolumeInfo> volumes) =>
        new(
            ResolveInitialPath(settings.LastLeftPanePath, volumes),
            ResolveInitialPath(settings.LastRightPanePath, volumes));

    private static NormalizedPath ResolveInitialPath(NormalizedPath? savedPath, IReadOnlyList<VolumeInfo> volumes)
    {
        if (savedPath is { } path)
        {
            return ResolveExistingFolderOrDrive(path.DisplayPath, volumes);
        }

        return GetFirstAvailableRoot(volumes);
    }

    private static NormalizedPath ResolveExistingFolderOrDrive(string path, IReadOnlyList<VolumeInfo> volumes)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return GetFirstAvailableRoot(volumes);
        }

        var current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return NormalizedPath.FromUserInput(current);
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return GetFirstAvailableRoot(volumes);
    }

    private static NormalizedPath GetFirstAvailableRoot(IReadOnlyList<VolumeInfo> volumes)
    {
        foreach (var volume in volumes)
        {
            if (Directory.Exists(volume.RootPath.DisplayPath))
            {
                return volume.RootPath;
            }
        }

        return NormalizedPath.FromUserInput(
            Directory.GetLogicalDrives().FirstOrDefault(Directory.Exists)
            ?? Path.GetPathRoot(Environment.SystemDirectory)
            ?? @"C:\");
    }
}

public sealed record StartupPanelPaths(NormalizedPath LeftPath, NormalizedPath RightPath);
