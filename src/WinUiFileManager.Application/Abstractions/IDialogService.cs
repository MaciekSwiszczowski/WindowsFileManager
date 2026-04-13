using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Abstractions;

public interface IDialogService
{
    Task<CollisionPolicy> ShowCollisionDialogAsync(
        NormalizedPath sourcePath,
        NormalizedPath destinationPath,
        CancellationToken ct);

    Task<bool> ShowDeleteConfirmationAsync(
        int itemCount,
        bool includesDirectories,
        CancellationToken ct);

    Task<string?> ShowCreateFolderDialogAsync(CancellationToken ct);

    Task<string?> ShowRenameDialogAsync(string currentName, CancellationToken ct);

    Task ShowPropertiesAsync(IReadOnlyList<FileSystemEntryModel> entries, CancellationToken ct);

    Task ShowOperationResultAsync(OperationSummary summary, CancellationToken ct);
}
