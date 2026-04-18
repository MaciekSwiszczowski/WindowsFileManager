using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Domain.Operations;

public sealed record OperationPlan
{
    public OperationPlan(
        OperationType type,
        IReadOnlyList<OperationItemPlan> items,
        NormalizedPath? destinationDirectory,
        CollisionPolicy collisionPolicy,
        ParallelExecutionOptions parallelOptions)
    {
        Type = type;
        Items = items;
        DestinationDirectory = destinationDirectory;
        CollisionPolicy = collisionPolicy;
        ParallelOptions = parallelOptions;
    }

    public OperationType Type { get; init; }

    public IReadOnlyList<OperationItemPlan> Items { get; init; }

    public NormalizedPath? DestinationDirectory { get; init; }

    public CollisionPolicy CollisionPolicy { get; init; }

    public ParallelExecutionOptions ParallelOptions { get; init; }
}
