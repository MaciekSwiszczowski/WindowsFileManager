using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IFileOperationPlanner
{
    Task<OperationPlan> PlanCopyAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CollisionPolicy collisionPolicy,
        ParallelExecutionOptions parallelOptions,
        CancellationToken cancellationToken);

    Task<OperationPlan> PlanMoveAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        NormalizedPath destination,
        CollisionPolicy collisionPolicy,
        ParallelExecutionOptions parallelOptions,
        CancellationToken cancellationToken);

    Task<OperationPlan> PlanDeleteAsync(
        IReadOnlyList<FileSystemEntryModel> items,
        CancellationToken cancellationToken);

    Task<OperationPlan> PlanCreateFolderAsync(
        NormalizedPath parentDirectory,
        string folderName,
        CancellationToken cancellationToken);
}
