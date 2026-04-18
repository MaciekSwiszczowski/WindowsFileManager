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
                {
                    continue;
                }

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
            return null;
        }
    }
}
