using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Domain.Results;

public sealed record OperationSummary
{
    public OperationSummary(
        OperationType type,
        OperationStatus status,
        int totalItems,
        int succeededCount,
        int failedCount,
        int warningCount,
        int skippedCount,
        bool wasCancelled,
        TimeSpan duration,
        IReadOnlyList<OperationItemResult> itemResults,
        string? message)
    {
        Type = type;
        Status = status;
        TotalItems = totalItems;
        SucceededCount = succeededCount;
        FailedCount = failedCount;
        WarningCount = warningCount;
        SkippedCount = skippedCount;
        WasCancelled = wasCancelled;
        Duration = duration;
        ItemResults = itemResults;
        Message = message;
    }

    public OperationType Type { get; init; }

    public OperationStatus Status { get; init; }

    public int TotalItems { get; init; }

    public int SucceededCount { get; init; }

    public int FailedCount { get; init; }

    public int WarningCount { get; init; }

    public int SkippedCount { get; init; }

    public bool WasCancelled { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<OperationItemResult> ItemResults { get; init; }

    public string? Message { get; init; }
}
