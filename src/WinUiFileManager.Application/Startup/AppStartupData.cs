namespace WinUiFileManager.Application.Startup;

/// <summary>
/// Aggregated data computed during application startup and handed to the shell: the loaded settings, the
/// discovered NTFS volumes, and the resolved initial directory for each pane. Delivered via
/// <see cref="AppStartupDataLoadedMessage"/>.
/// </summary>
/// <param name="Settings">The settings loaded for this session.</param>
/// <param name="NtfsVolumes">NTFS volumes available at launch.</param>
/// <param name="LeftInitialPath">Resolved starting directory for the left pane.</param>
/// <param name="RightInitialPath">Resolved starting directory for the right pane.</param>
public sealed record AppStartupData(
    AppSettings Settings,
    IReadOnlyList<VolumeInfo> NtfsVolumes,
    NormalizedPath LeftInitialPath,
    NormalizedPath RightInitialPath);
