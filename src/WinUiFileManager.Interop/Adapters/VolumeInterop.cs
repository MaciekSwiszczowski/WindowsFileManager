using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public sealed class VolumeInterop : IVolumeInterop
{
    public IReadOnlyList<DriveVolumeInfo> GetVolumes()
    {
        var volumes = new List<DriveVolumeInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                    continue;

                volumes.Add(new DriveVolumeInfo(
                    DriveLetter: drive.Name[..1],
                    Label: drive.VolumeLabel,
                    FileSystemName: drive.DriveFormat,
                    RootPath: drive.RootDirectory.FullName,
                    SerialNumber: 0,
                    MaxComponentLength: 255,
                    FileSystemFlags: 0));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return volumes;
    }

    private const string ExtendedPathPrefix = @"\\?\";

    public DriveVolumeInfo? GetVolumeForPath(string path)
    {
        var cleaned = path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? path[ExtendedPathPrefix.Length..]
            : path;

        var fullPath = Path.GetFullPath(cleaned);
        var root = Path.GetPathRoot(fullPath);

        if (root is null || root.Length < 2)
            return null;

        try
        {
            var driveInfo = new DriveInfo(root);
            if (!driveInfo.IsReady)
                return null;

            return new DriveVolumeInfo(
                DriveLetter: driveInfo.Name[..1],
                Label: driveInfo.VolumeLabel,
                FileSystemName: driveInfo.DriveFormat,
                RootPath: driveInfo.RootDirectory.FullName,
                SerialNumber: 0,
                MaxComponentLength: 255,
                FileSystemFlags: 0);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
