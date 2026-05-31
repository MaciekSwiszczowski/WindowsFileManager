using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction for enumerating drive volumes and mapping a path to its owning volume. Implemented by
/// <see cref="VolumeInterop"/>; consumed by the NTFS volume policy service in Infrastructure.
/// </summary>
/// <remarks>
/// Implementations may populate only the identifying fields of <see cref="DriveVolumeInfo"/> (drive letter,
/// label, file-system name, root) and leave serial/component-length/flags as placeholders — see
/// <see cref="VolumeInterop"/>.
/// </remarks>
public interface IVolumeInterop
{
    /// <summary>Returns one <see cref="DriveVolumeInfo"/> per ready drive; never <see langword="null"/>.</summary>
    IReadOnlyList<DriveVolumeInfo> GetVolumes();

    /// <summary>Resolves the volume that owns <paramref name="path"/>, or <see langword="null"/> if it cannot be determined.</summary>
    /// <param name="path">A path; the extended-length <c>\\?\</c> prefix (if present) is handled by the implementation.</param>
    DriveVolumeInfo? GetVolumeForPath(string path);
}
