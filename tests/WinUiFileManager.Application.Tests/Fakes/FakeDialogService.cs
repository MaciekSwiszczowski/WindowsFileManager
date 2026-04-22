using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeDialogService : IDialogService
{
    public bool DeleteConfirmationResult { get; set; } = true;
    public string? CreateFolderResult { get; set; } = "NewFolder";
    public CollisionPolicy CollisionResult { get; set; } = CollisionPolicy.Overwrite;

    public int DeleteConfirmationCallCount { get; private set; }
    public int CreateFolderDialogCallCount { get; private set; }
    public int ShowOperationProgressCallCount { get; private set; }
    public int ShowOperationResultCallCount { get; private set; }
    public OperationSummary? LastOperationResult { get; private set; }

    public Task<CollisionPolicy> ShowCollisionDialogAsync(NormalizedPath sourcePath, NormalizedPath destinationPath, CancellationToken ct)
    {
        return Task.FromResult(CollisionResult);
    }

    public Task<bool> ShowDeleteConfirmationAsync(int itemCount, bool includesDirectories, CancellationToken ct)
    {
        DeleteConfirmationCallCount++;
        return Task.FromResult(DeleteConfirmationResult);
    }

    public Task<string?> ShowCreateFolderDialogAsync(CancellationToken ct)
    {
        CreateFolderDialogCallCount++;
        return Task.FromResult(CreateFolderResult);
    }

    public Task<IOperationProgressDialog> ShowOperationProgressAsync(
        OperationType operationType,
        Action onCancel,
        CancellationToken ct)
    {
        ShowOperationProgressCallCount++;
        return Task.FromResult<IOperationProgressDialog>(new FakeOperationProgressDialog());
    }

    public Task ShowOperationResultAsync(OperationSummary summary, CancellationToken ct)
    {
        ShowOperationResultCallCount++;
        LastOperationResult = summary;
        return Task.CompletedTask;
    }

    private sealed class FakeOperationProgressDialog : IOperationProgressDialog
    {
        public void ReportProgress(OperationProgressEvent progressEvent)
        {
        }

        public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
