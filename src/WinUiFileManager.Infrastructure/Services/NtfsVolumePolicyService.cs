using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Enforces the app's NTFS-only policy: enumerates the NTFS volumes and decides whether a given path lives on a
/// supported (NTFS) volume. Infrastructure implementation of <see cref="INtfsVolumePolicyService"/>, backed by
/// <see cref="IVolumeInterop"/>.
/// </summary>
/// <remarks>
/// The underlying volume enumeration is synchronous (BCL <c>DriveInfo</c>); the async members wrap the result in
/// a completed task to satisfy the abstraction without a fake <c>Async</c> suffix on real I/O. Cancellation is
/// honored before doing the (fast) work. The <see cref="VolumeInfo.SerialNumber"/>-style fields are not used here;
/// only the file-system name matters for the NTFS decision.
/// </remarks>
internal sealed class NtfsVolumePolicyService : INtfsVolumePolicyService
{
    private readonly IVolumeInterop _volumeInterop;

    public NtfsVolumePolicyService(IVolumeInterop volumeInterop)
    {
        _volumeInterop = volumeInterop;
    }

    /// <summary>Returns the subset of ready volumes whose file system is NTFS, projected to domain <see cref="VolumeInfo"/>.</summary>
    /// <param name="cancellationToken">Checked before enumeration.</param>
    /// <returns>A completed task with the NTFS volumes (never <see langword="null"/>; may be empty).</returns>
    /// <exception cref="OperationCanceledException">If cancellation is requested before work begins.</exception>
    public Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var volumes = _volumeInterop.GetVolumes();

        // The trailing `true` is VolumeInfo.IsNtfs — by construction every projected volume passed the IsNtfs filter.
        var ntfsVolumes = volumes
            .Where(v => IsNtfs(v.FileSystemName))
            .Select(v => new VolumeInfo(
                v.DriveLetter,
                v.Label,
                v.FileSystemName,
                NormalizedPath.FromUserInput(v.RootPath),
                true))
            .ToList();

        return Task.FromResult<IReadOnlyList<VolumeInfo>>(ntfsVolumes);
    }

    /// <summary>Reports whether <paramref name="path"/> resides on an NTFS volume.</summary>
    /// <param name="path">The path to test (its owning volume is resolved internally).</param>
    /// <param name="cancellationToken">Checked before resolution.</param>
    /// <returns>A completed task: <see langword="true"/> when the owning volume is NTFS; <see langword="false"/> otherwise (including unknown volume).</returns>
    public Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var volume = _volumeInterop.GetVolumeForPath(path);
        var isNtfs = volume is not null && IsNtfs(volume.FileSystemName);
        return Task.FromResult(isNtfs);
    }

    /// <summary>
    /// Validates <paramref name="path"/> for use by the app, producing a user-facing reason on failure (empty,
    /// unknown volume, or non-NTFS volume).
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <returns>A <see cref="PathValidationResult"/>; <see cref="PathValidationResult.Valid"/> only for NTFS paths.</returns>
    public PathValidationResult ValidateNtfsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Invalid("Path cannot be empty.");
        }

        try
        {
            var volume = _volumeInterop.GetVolumeForPath(path);

            if (volume is null)
            {
                return PathValidationResult.Invalid(
                    $"Could not determine volume for path '{path}'.");
            }

            if (!IsNtfs(volume.FileSystemName))
            {
                return PathValidationResult.Invalid(
                    $"Path '{path}' is on a non-NTFS volume ({volume.FileSystemName}). Only NTFS volumes are supported.");
            }

            return PathValidationResult.Valid();
        }
        catch (Exception ex)
        {
            // Any failure resolving the volume is surfaced as an invalid path with the underlying message, rather
            // than propagating — callers treat validation purely via the returned result.
            return PathValidationResult.Invalid($"Failed to validate path: {ex.Message}");
        }
    }

    // NTFS is matched case-insensitively against the volume's file-system name; this single predicate is the policy.
    private static bool IsNtfs(string fileSystemName) =>
        string.Equals(fileSystemName, "NTFS", StringComparison.OrdinalIgnoreCase);
}
