using WinUiFileManager.Application.Validation;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Abstractions;

public interface INtfsVolumePolicyService
{
    Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken);
    Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken);
    PathValidationResult ValidateNtfsPath(string path);
}
