namespace WinUiFileManager.Interop.Types;

public sealed record DriveVolumeInfo(
    string DriveLetter,
    string Label,
    string FileSystemName,
    string RootPath,
    uint SerialNumber,
    uint MaxComponentLength,
    uint FileSystemFlags);
