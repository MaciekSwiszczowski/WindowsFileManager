namespace WinUiFileManager.Application.Startup;

public sealed record AppStartupData(
    AppSettings Settings,
    IReadOnlyList<VolumeInfo> NtfsVolumes,
    NormalizedPath LeftInitialPath,
    NormalizedPath RightInitialPath);
