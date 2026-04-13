using WinUiFileManager.Domain.Results;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class FakeDialogService : IDialogService
{
    public bool DeleteConfirmationResult { get; set; } = true;
    public string? CreateFolderResult { get; set; } = "NewFolder";
    public string? RenameResult { get; set; } = "renamed.txt";
    public CollisionPolicy CollisionResult { get; set; } = CollisionPolicy.Overwrite;

    public int DeleteConfirmationCallCount { get; private set; }
    public int CreateFolderDialogCallCount { get; private set; }
    public int RenameDialogCallCount { get; private set; }
    public int ShowOperationResultCallCount { get; private set; }
    public int ShowPropertiesCallCount { get; private set; }
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

    public Task<string?> ShowRenameDialogAsync(string currentName, CancellationToken ct)
    {
        RenameDialogCallCount++;
        return Task.FromResult(RenameResult);
    }

    public Task ShowPropertiesAsync(IReadOnlyList<FileSystemEntryModel> entries, CancellationToken ct)
    {
        ShowPropertiesCallCount++;
        return Task.CompletedTask;
    }

    public Task ShowOperationResultAsync(OperationSummary summary, CancellationToken ct)
    {
        ShowOperationResultCallCount++;
        LastOperationResult = summary;
        return Task.CompletedTask;
    }
}
