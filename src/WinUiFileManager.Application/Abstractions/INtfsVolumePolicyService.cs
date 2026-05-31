using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Enforces the app's NTFS-only policy: enumerates NTFS volumes and validates that a path lives on one.
/// Implemented in Infrastructure; used at startup and before navigation.
/// </summary>
public interface INtfsVolumePolicyService
{
    /// <summary>Returns the NTFS volumes currently mounted on the machine.</summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken);

    /// <summary>Determines whether <paramref name="path"/> resides on an NTFS volume.</summary>
    /// <param name="path">The path to test.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> if the path is on an NTFS volume; otherwise <see langword="false"/>.</returns>
    Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken);

    /// <summary>Synchronously validates that <paramref name="path"/> is acceptable under the NTFS-only policy.</summary>
    /// <returns>A valid result, or an invalid one carrying a user-facing message.</returns>
    PathValidationResult ValidateNtfsPath(string path);
}
