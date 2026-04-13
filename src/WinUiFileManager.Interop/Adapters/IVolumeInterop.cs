using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public interface IVolumeInterop
{
    IReadOnlyList<DriveVolumeInfo> GetVolumes();
    DriveVolumeInfo? GetVolumeForPath(string path);
}
