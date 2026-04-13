using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.FileOperations;

public sealed class MoveSelectionCommandHandler
{
    private readonly IFileOperationPlanner _planner;
    private readonly IFileOperationService _operationService;
    private readonly ILogger<MoveSelectionCommandHandler> _logger;

    public MoveSelectionCommandHandler(
        IFileOperationPlanner planner,
        IFileOperationService operationService,
        ILogger<MoveSelectionCommandHandler> logger)
    {
        _planner = planner;
        _operationService = operationService;
        _logger = logger;
    }

    public async Task<OperationSummary> ExecuteAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CollisionPolicy policy,
        ParallelExecutionOptions parallelOptions,
        IProgress<OperationProgressEvent> progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Planning move of {Count} items to {Destination}", items.Count, destination);
        var plan = await _planner.PlanMoveAsync(items, destination, policy, parallelOptions, ct);

        _logger.LogInformation("Executing move plan with {ItemCount} planned items", plan.Items.Count);
        return await _operationService.ExecuteAsync(plan, progress, ct);
    }
}
