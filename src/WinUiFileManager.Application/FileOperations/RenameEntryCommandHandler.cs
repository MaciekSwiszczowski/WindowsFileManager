using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.FileOperations;

public sealed class RenameEntryCommandHandler
{
    private readonly IFileOperationService _operationService;
    private readonly ILogger<RenameEntryCommandHandler> _logger;

    public RenameEntryCommandHandler(
        IFileOperationService operationService,
        ILogger<RenameEntryCommandHandler> logger)
    {
        _operationService = operationService;
        _logger = logger;
    }

    public Task<OperationSummary> ExecuteAsync(
        FileSystemEntryModel entry,
        string newName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New name cannot be empty.", nameof(newName));
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("New name contains invalid characters.", nameof(newName));
        }

        var parentDir = Path.GetDirectoryName(entry.FullPath.DisplayPath)
                        ?? throw new InvalidOperationException($"Cannot determine parent directory of '{entry.FullPath.DisplayPath}'.");

        var newFullPath = Path.Combine(parentDir, newName);

        _logger.LogInformation("Renaming {OldName} to {NewName}", entry.Name, newName);

        var planItem = new OperationItemPlan(
            entry.FullPath,
            NormalizedPath.FromUserInput(newFullPath),
            entry.Kind,
            entry.Size);

        var plan = new OperationPlan(
            OperationType.Rename,
            [planItem],
            NormalizedPath.FromUserInput(parentDir),
            CollisionPolicy.Ask,
            new ParallelExecutionOptions());

        return _operationService.ExecuteAsync(plan, new Progress<OperationProgressEvent>(), ct);
    }
}
