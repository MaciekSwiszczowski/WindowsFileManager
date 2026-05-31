namespace WinUiFileManager.App.Startup;

using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;

/// <summary>
/// Resolves the folder each pane should open to at startup, given persisted settings and the available
/// volumes. Pure decision logic in the App layer; performs only read-only filesystem existence checks.
/// </summary>
/// <remarks>
/// Always yields a usable path: a saved path that no longer exists is walked up to its nearest existing
/// ancestor, and if nothing matches it falls back to the first available volume root (and ultimately a
/// hard-coded drive). Threading: callers invoke this from the background startup chain; it touches the
/// filesystem synchronously but does no UI work.
/// </remarks>
public sealed class StartupPathResolver
{
    /// <summary>
    /// Resolves both panes' initial paths from the saved per-pane paths, falling back as needed.
    /// </summary>
    /// <param name="settings">Persisted app settings carrying the last-used pane paths (may be null entries).</param>
    /// <param name="volumes">Volumes used as fallback roots when a saved path is missing or gone.</param>
    /// <returns>The resolved left/right startup paths; never empty.</returns>
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
        // Walk up the saved path until an existing directory is found; a deleted/renamed leaf shouldn't
        // strand the pane on a non-existent location.
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

        // Last-ditch fallback when no enumerated volume root exists: first logical drive, then the
        // system drive root, then a hard-coded C:\ so the app always has somewhere to open.
        return NormalizedPath.FromUserInput(
            Directory.GetLogicalDrives().FirstOrDefault(Directory.Exists)
            ?? Path.GetPathRoot(Environment.SystemDirectory)
            ?? @"C:\");
    }
}

/// <summary>
/// Immutable pair of resolved startup paths, one per pane, produced by <see cref="StartupPathResolver"/>.
/// </summary>
public sealed record StartupPanelPaths(NormalizedPath LeftPath, NormalizedPath RightPath);
