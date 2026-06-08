namespace WinUiFileManager.App.Startup;

using Application.FileEntries;

/// <summary>
/// Immutable pair of resolved startup paths, one per pane, produced by <see cref="StartupPathResolver"/>.
/// </summary>
public sealed record StartupPanelPaths(NormalizedPath LeftPath, NormalizedPath RightPath);
