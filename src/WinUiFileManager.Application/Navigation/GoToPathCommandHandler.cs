using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed class GoToPathCommandHandler
{
    private readonly IFileSystemService _fileSystemService;
    private readonly INtfsVolumePolicyService _ntfsVolumePolicyService;
    private readonly IPathNormalizationService _pathNormalizationService;
    private readonly ILogger<GoToPathCommandHandler> _logger;

    public GoToPathCommandHandler(
        IFileSystemService fileSystemService,
        INtfsVolumePolicyService ntfsVolumePolicyService,
        IPathNormalizationService pathNormalizationService,
        ILogger<GoToPathCommandHandler> logger)
    {
        _fileSystemService = fileSystemService;
        _ntfsVolumePolicyService = ntfsVolumePolicyService;
        _pathNormalizationService = pathNormalizationService;
        _logger = logger;
    }

    public async Task<GoToPathResult> ExecuteAsync(string rawPath, CancellationToken ct)
    {
        var pathValidation = _pathNormalizationService.Validate(rawPath);
        if (!pathValidation.IsValid)
        {
            return new GoToPathResult(false, null, null, pathValidation.ErrorMessage);
        }

        var normalizedPath = _pathNormalizationService.Normalize(rawPath);

        var ntfsValidation = _ntfsVolumePolicyService.ValidateNtfsPath(normalizedPath.Value);
        if (!ntfsValidation.IsValid)
        {
            return new GoToPathResult(false, null, null, ntfsValidation.ErrorMessage);
        }

        var exists = await _fileSystemService.DirectoryExistsAsync(normalizedPath, ct);
        if (!exists)
        {
            _logger.LogWarning("Directory does not exist: {Path}", normalizedPath);
            return new GoToPathResult(false, normalizedPath, null, $"Directory not found: {normalizedPath.DisplayPath}");
        }

        _logger.LogDebug("Navigating to path: {Path}", normalizedPath);
        var entries = await _fileSystemService.EnumerateDirectoryAsync(normalizedPath, ct);
        return new GoToPathResult(true, normalizedPath, entries, null);
    }
}
