using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class NtfsVolumePolicyService : INtfsVolumePolicyService
{
    private readonly IVolumeInterop _volumeInterop;

    public NtfsVolumePolicyService(IVolumeInterop volumeInterop)
    {
        _volumeInterop = volumeInterop;
    }

    public Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var volumes = _volumeInterop.GetVolumes();

        var ntfsVolumes = volumes
            .Where(v => IsNtfs(v.FileSystemName))
            .Select(v => new VolumeInfo(
                v.DriveLetter,
                v.Label,
                v.FileSystemName,
                NormalizedPath.FromUserInput(v.RootPath),
                IsNtfs: true))
            .ToList();

        return Task.FromResult<IReadOnlyList<VolumeInfo>>(ntfsVolumes);
    }

    public Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var volume = _volumeInterop.GetVolumeForPath(path);
        var isNtfs = volume is not null && IsNtfs(volume.FileSystemName);
        return Task.FromResult(isNtfs);
    }

    public PathValidationResult ValidateNtfsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return PathValidationResult.Invalid("Path cannot be empty.");

        try
        {
            var volume = _volumeInterop.GetVolumeForPath(path);

            if (volume is null)
                return PathValidationResult.Invalid(
                    $"Could not determine volume for path '{path}'.");

            if (!IsNtfs(volume.FileSystemName))
                return PathValidationResult.Invalid(
                    $"Path '{path}' is on a non-NTFS volume ({volume.FileSystemName}). Only NTFS volumes are supported.");

            return PathValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return PathValidationResult.Invalid($"Failed to validate path: {ex.Message}");
        }
    }

    private static bool IsNtfs(string fileSystemName) =>
        string.Equals(fileSystemName, "NTFS", StringComparison.OrdinalIgnoreCase);
}
