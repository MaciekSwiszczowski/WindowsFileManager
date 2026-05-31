namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Immutable description of a drive volume (drive letter, label, file-system name, root path, and identity
/// fields). Produced by <see cref="Adapters.IVolumeInterop"/> and consumed by the NTFS volume policy service.
/// </summary>
/// <remarks>
/// Depending on the producer, <see cref="SerialNumber"/>, <see cref="MaxComponentLength"/>, and
/// <see cref="FileSystemFlags"/> may be placeholder values (see <see cref="Adapters.VolumeInterop"/>); only the
/// drive letter, label, file-system name, and root path are guaranteed real there.
/// </remarks>
public sealed record DriveVolumeInfo
{
    /// <summary>Creates a volume description. See the property docs for field semantics and placeholder caveats.</summary>
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

    /// <summary>The single drive letter (e.g. <c>"C"</c>), without colon or separator.</summary>
    public string DriveLetter { get; init; }

    /// <summary>The volume label, possibly empty.</summary>
    public string Label { get; init; }

    /// <summary>The file-system name (e.g. <c>"NTFS"</c>), used by the volume policy to gate NTFS-only features.</summary>
    public string FileSystemName { get; init; }

    /// <summary>The volume root directory path (e.g. <c>"C:\"</c>).</summary>
    public string RootPath { get; init; }

    /// <summary>The volume serial number; may be a placeholder (<c>0</c>) depending on the producer.</summary>
    public uint SerialNumber { get; init; }

    /// <summary>Maximum file-name component length; may be a placeholder (<c>255</c>) depending on the producer.</summary>
    public uint MaxComponentLength { get; init; }

    /// <summary>File-system capability flags; may be a placeholder (<c>0</c>) depending on the producer.</summary>
    public uint FileSystemFlags { get; init; }
}
