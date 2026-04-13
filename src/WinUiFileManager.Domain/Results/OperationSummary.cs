using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Domain.Results;

public sealed record OperationSummary(
    OperationType Type,
    OperationStatus Status,
    int TotalItems,
    int SucceededCount,
    int FailedCount,
    int WarningCount,
    int SkippedCount,
    bool WasCancelled,
    TimeSpan Duration,
    IReadOnlyList<OperationItemResult> ItemResults,
    string? Message);
