using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Operations;

public sealed record OperationPlan(
    OperationType Type,
    IReadOnlyList<OperationItemPlan> Items,
    NormalizedPath? DestinationDirectory,
    CollisionPolicy CollisionPolicy,
    ParallelExecutionOptions ParallelOptions);
