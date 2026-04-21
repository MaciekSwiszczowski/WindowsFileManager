using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Navigation;

public sealed class RefreshPaneCommandHandler
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogger<RefreshPaneCommandHandler> _logger;

    public RefreshPaneCommandHandler(
        IFileSystemService fileSystemService,
        ILogger<RefreshPaneCommandHandler> logger)
    {
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public Task<IReadOnlyList<FileSystemEntryModel>> ExecuteAsync(
        NormalizedPath currentPath,
        CancellationToken ct)
    {
        _logger.LogDebug("Refreshing pane for path: {Path}", currentPath);
        return _fileSystemService.EnumerateDirectoryAsync(currentPath, ct);
    }
}
