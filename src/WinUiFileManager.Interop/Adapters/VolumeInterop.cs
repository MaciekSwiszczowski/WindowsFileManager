// RS0030 (banned-symbol) is suppressed because this adapter intentionally uses BCL DriveInfo rather than the
// banned raw Win32 entry points: VolumeInterop is the adapter behind the cached NTFS volume policy service.
#pragma warning disable RS0030 // VolumeInterop is the adapter behind the cached NTFS volume policy service.
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Adapter that enumerates the machine's ready drives and resolves the volume that owns a given path.
/// Implements <see cref="IVolumeInterop"/> on top of the BCL <see cref="DriveInfo"/> API. Used by the cached
/// NTFS volume policy service to decide whether a path lives on a supported (NTFS) volume.
/// </summary>
/// <remarks>
/// PLACEHOLDER FIELDS: <see cref="DriveVolumeInfo.SerialNumber"/>, <see cref="DriveVolumeInfo.MaxComponentLength"/>,
/// and <see cref="DriveVolumeInfo.FileSystemFlags"/> are not read from the volume here — they are hardcoded
/// (serial = 0, max component length = 255, flags = 0). Only drive letter, label, file-system name, and root
/// path are real. Callers that need an accurate volume serial must use
/// <see cref="IFileSystemMetadataInterop.TryGetVolumeSerialHex"/> instead. Drives that are not ready, or that
/// throw <see cref="IOException"/>/<see cref="UnauthorizedAccessException"/> while probing, are silently skipped.
/// </remarks>
internal sealed class VolumeInterop : IVolumeInterop
{
    /// <summary>Enumerates all ready drives as <see cref="DriveVolumeInfo"/> records.</summary>
    /// <returns>
    /// One entry per ready drive. Never <see langword="null"/>; may be empty. Per-drive failures
    /// (not ready, I/O, access denied) are swallowed so one bad drive cannot break enumeration.
    /// </returns>
    public IReadOnlyList<DriveVolumeInfo> GetVolumes()
    {
        var volumes = new List<DriveVolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                // Serial (0), max component length (255), and flags (0) are placeholders — see the type remarks.
                volumes.Add(new DriveVolumeInfo(
                    drive.Name[..1],
                    drive.VolumeLabel,
                    drive.DriveFormat,
                    drive.RootDirectory.FullName,
                    0,
                    255,
                    0));
            }
            catch (IOException)
            {
                // Drive became unavailable between GetDrives() and probing; skip it.
            }
            catch (UnauthorizedAccessException)
            {
                // No rights to read this volume's metadata; skip it.
            }
        }

        return volumes;
    }

    private const string ExtendedPathPrefix = @"\\?\";

    /// <summary>Resolves the volume that contains <paramref name="path"/>.</summary>
    /// <param name="path">A path which may carry the extended-length <c>\\?\</c> prefix; it is stripped first.</param>
    /// <returns>
    /// The owning volume, or <see langword="null"/> if the root cannot be determined, the drive is not ready,
    /// or any error occurs. As with <see cref="GetVolumes"/>, the serial/component-length/flags fields are placeholders.
    /// </returns>
    public DriveVolumeInfo? GetVolumeForPath(string path)
    {
        // DriveInfo cannot parse the \\?\ extended-length form, so strip the prefix before resolving the root.
        var cleaned = path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? path[ExtendedPathPrefix.Length..]
            : path;

        var fullPath = Path.GetFullPath(cleaned);
        var root = Path.GetPathRoot(fullPath);

        // A length < 2 rules out a usable drive root such as "C:" (UNC/relative roots are not supported here).
        if (root is null || root.Length < 2)
        {
            return null;
        }

        try
        {
            var driveInfo = new DriveInfo(root);
            if (!driveInfo.IsReady)
            {
                return null;
            }

            // Placeholder serial/component-length/flags — see the type remarks.
            return new DriveVolumeInfo(
                driveInfo.Name[..1],
                driveInfo.VolumeLabel,
                driveInfo.DriveFormat,
                driveInfo.RootDirectory.FullName,
                0,
                255,
                0);
        }
        catch (Exception)
        {
            // Any failure resolving the drive (removed media, access denied, malformed root) maps to "unknown".
            return null;
        }
    }
}
