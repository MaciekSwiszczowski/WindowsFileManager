using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.FileOperations;

public sealed class DeleteSelectionCommandHandler
{
    private readonly IFileOperationPlanner _planner;
    private readonly IFileOperationService _operationService;
    private readonly ILogger<DeleteSelectionCommandHandler> _logger;

    public DeleteSelectionCommandHandler(
        IFileOperationPlanner planner,
        IFileOperationService operationService,
        ILogger<DeleteSelectionCommandHandler> logger)
    {
        _planner = planner;
        _operationService = operationService;
        _logger = logger;
    }

    public async Task<OperationSummary> ExecuteAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        IProgress<OperationProgressEvent> progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Planning deletion of {Count} items", items.Count);
        var plan = await _planner.PlanDeleteAsync(items, ct);

        _logger.LogInformation("Executing delete plan with {ItemCount} planned items", plan.Items.Count);
        return await _operationService.ExecuteAsync(plan, progress, ct);
    }
}
