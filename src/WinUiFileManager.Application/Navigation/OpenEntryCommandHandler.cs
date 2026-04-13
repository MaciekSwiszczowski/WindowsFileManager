using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed class OpenEntryCommandHandler
{
    private readonly IFileSystemService _fileSystemService;
    private readonly INtfsVolumePolicyService _ntfsVolumePolicyService;
    private readonly IShellService _shellService;
    private readonly ILogger<OpenEntryCommandHandler> _logger;

    public OpenEntryCommandHandler(
        IFileSystemService fileSystemService,
        INtfsVolumePolicyService ntfsVolumePolicyService,
        IShellService shellService,
        ILogger<OpenEntryCommandHandler> logger)
    {
        _fileSystemService = fileSystemService;
        _ntfsVolumePolicyService = ntfsVolumePolicyService;
        _shellService = shellService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileSystemEntryModel>> ExecuteAsync(
        NormalizedPath path,
        CancellationToken ct)
    {
        var validation = _ntfsVolumePolicyService.ValidateNtfsPath(path.Value);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Path is not a valid NTFS path: {Path}, Error: {Error}", path, validation.ErrorMessage);
            throw new InvalidOperationException(validation.ErrorMessage);
        }

        var entry = await _fileSystemService.GetEntryAsync(path, ct);
        if (entry is null)
        {
            _logger.LogWarning("Entry does not exist: {Path}", path);
            throw new FileNotFoundException($"Entry not found: {path.DisplayPath}");
        }

        if (entry.Kind == ItemKind.Directory)
        {
            _logger.LogDebug("Enumerating directory: {Path}", path);
            return await _fileSystemService.EnumerateDirectoryAsync(path, ct);
        }
        else
        {
            _logger.LogDebug("Opening file: {Path}", path);
            await _shellService.OpenWithDefaultAppAsync(path, ct);
            return Array.Empty<FileSystemEntryModel>();
        }
    }
}
