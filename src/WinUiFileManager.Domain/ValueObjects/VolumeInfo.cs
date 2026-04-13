namespace WinUiFileManager.Domain.ValueObjects;

public sealed record VolumeInfo(
    string DriveLetter,
    string Label,
    string FileSystemName,
    NormalizedPath RootPath,
    bool IsNtfs);
