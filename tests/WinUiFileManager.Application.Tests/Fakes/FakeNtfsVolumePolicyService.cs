using WinUiFileManager.Domain.Errors;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeNtfsVolumePolicyService : INtfsVolumePolicyService
{
    public Task<IReadOnlyList<VolumeInfo>> GetNtfsVolumesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<VolumeInfo> volumes =
        [
            new(
                "C",
                "System",
                "NTFS",
                NormalizedPath.FromUserInput(@"C:\"),
                isNtfs: true),
        ];

        return Task.FromResult(volumes);
    }

    public Task<bool> IsNtfsPathAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public PathValidationResult ValidateNtfsPath(string path) => PathValidationResult.Valid();
}
