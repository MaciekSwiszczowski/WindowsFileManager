using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.FileOperations;

public sealed class CreateFolderCommandHandler
{
    private readonly IFileOperationPlanner _planner;
    private readonly IFileOperationService _operationService;
    private readonly ILogger<CreateFolderCommandHandler> _logger;

    public CreateFolderCommandHandler(
        IFileOperationPlanner planner,
        IFileOperationService operationService,
        ILogger<CreateFolderCommandHandler> logger)
    {
        _planner = planner;
        _operationService = operationService;
        _logger = logger;
    }

    public async Task<OperationSummary> ExecuteAsync(
        NormalizedPath parentDirectory,
        string folderName,
        IProgress<OperationProgressEvent> progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Planning folder creation: {FolderName} in {Parent}", folderName, parentDirectory);
        var plan = await _planner.PlanCreateFolderAsync(parentDirectory, folderName, ct);

        _logger.LogInformation("Executing create folder plan");
        return await _operationService.ExecuteAsync(plan, progress, ct);
    }
}
