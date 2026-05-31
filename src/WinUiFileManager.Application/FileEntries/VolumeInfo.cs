namespace WinUiFileManager.Application.FileEntries;

/// <summary>
/// Describes a mounted volume/drive discovered at startup, used to validate NTFS-only navigation
/// and to populate drive pickers. Produced by <see cref="WinUiFileManager.Application.Abstractions.INtfsVolumePolicyService"/>.
/// </summary>
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

    /// <summary>The drive letter (e.g. <c>C:</c>).</summary>
    public string DriveLetter { get; init; }

    /// <summary>The user-assigned volume label, or empty when none is set.</summary>
    public string Label { get; init; }

    /// <summary>The file-system name as reported by the OS (e.g. <c>NTFS</c>, <c>exFAT</c>).</summary>
    public string FileSystemName { get; init; }

    /// <summary>The normalized root path of the volume (e.g. <c>C:\</c>).</summary>
    public NormalizedPath RootPath { get; init; }

    /// <summary>Whether the volume is NTFS; non-NTFS volumes are rejected for navigation (NTFS-only app).</summary>
    public bool IsNtfs { get; init; }
}
