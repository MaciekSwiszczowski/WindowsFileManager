using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed class NavigateUpCommandHandler
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<NavigateUpCommandHandler> _logger;

    public NavigateUpCommandHandler(
        IFileSystemService fileSystemService,
        ILogger<NavigateUpCommandHandler> logger)
    {
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<(NormalizedPath Path, IReadOnlyList<FileSystemEntryModel> Entries)?> ExecuteAsync(
        NormalizedPath currentPath,
        CancellationToken ct)
    {
        var parent = Path.GetDirectoryName(currentPath.Value);
        if (parent is null)
        {
            _logger.LogDebug("No parent directory for: {Path}", currentPath);
            return null;
        }

        var parentPath = NormalizedPath.FromUserInput(parent);
        _logger.LogDebug("Navigating up from {Current} to {Parent}", currentPath, parentPath);

        var entries = await _fileSystemService.EnumerateDirectoryAsync(parentPath, ct);
        return (parentPath, entries);
    }
}
