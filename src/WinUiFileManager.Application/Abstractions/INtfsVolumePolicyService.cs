using WinUiFileManager.Domain.Errors;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface INtfsVolumePolicyService
{
    Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken);
    Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken);
    PathValidationResult ValidateNtfsPath(string path);
}
