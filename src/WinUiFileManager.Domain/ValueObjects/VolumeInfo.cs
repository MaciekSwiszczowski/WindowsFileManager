namespace WinUiFileManager.Domain.ValueObjects;

public sealed record VolumeInfo
{
    public VolumeInfo(
        string driveLetter,
        string label,
        string fileSystemName,
        NormalizedPath rootPath,
        bool isNtfs)
    {
        DriveLetter = driveLetter;
        Label = label;
        FileSystemName = fileSystemName;
        RootPath = rootPath;
        IsNtfs = isNtfs;
    }

    public string DriveLetter { get; init; }

    public string Label { get; init; }

    public string FileSystemName { get; init; }

    public NormalizedPath RootPath { get; init; }

    public bool IsNtfs { get; init; }
}
