namespace WinUiFileManager.Interop.Types;

public sealed record DriveVolumeInfo
{
    public DriveVolumeInfo(
        string driveLetter,
        string label,
        string fileSystemName,
        string rootPath,
        uint serialNumber,
        uint maxComponentLength,
        uint fileSystemFlags)
    {
        DriveLetter = driveLetter;
        Label = label;
        FileSystemName = fileSystemName;
        RootPath = rootPath;
        SerialNumber = serialNumber;
        MaxComponentLength = maxComponentLength;
        FileSystemFlags = fileSystemFlags;
    }

    public string DriveLetter { get; init; }

    public string Label { get; init; }

    public string FileSystemName { get; init; }

    public string RootPath { get; init; }

    public uint SerialNumber { get; init; }

    public uint MaxComponentLength { get; init; }

    public uint FileSystemFlags { get; init; }
}
