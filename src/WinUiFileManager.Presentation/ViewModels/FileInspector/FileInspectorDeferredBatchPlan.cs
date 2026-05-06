using System.Runtime.CompilerServices;
using WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorDeferredBatchPlan
{
    private static readonly TimeSpan DeferredLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IReadOnlyList<FileInspectorDeferredBatchDefinition> _batches;
    private readonly Func<bool> _isDisposed;

    public FileInspectorDeferredBatchPlan(
        IFileIdentityService fileIdentityService,
        ILogger<FileInspectorViewModel> logger,
        Func<bool> isDisposed)
    {
        _isDisposed = isDisposed;
        _batches =
        [
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Ids,
                IsFinalBatch: false,
                (selection, token) => IdentityFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Locks,
                IsFinalBatch: false,
                (selection, token) => LocksFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Links,
                IsFinalBatch: false,
                (selection, token) => LinksFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Streams,
                IsFinalBatch: false,
                (selection, token) => StreamsFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Security,
                IsFinalBatch: false,
                (selection, token) => SecurityFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Thumbnails,
                IsFinalBatch: false,
                (selection, token) => ThumbnailsFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token)),
            new FileInspectorDeferredBatchDefinition(
                FileInspectorCategory.Cloud,
                IsFinalBatch: true,
                (selection, token) => CloudFileInspectorCategory.LoadAsync(
                    fileIdentityService,
                    logger,
                    selection,
                    DeferredLoadTimeout,
                    token))
        ];
    }

    public async IAsyncEnumerable<FileInspectorDeferredBatchResult> LoadAsync(
        FileInspectorSelection selection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_isDisposed() || !selection.CanLoadDeferred)
        {
            yield break;
        }

        foreach (var batch in _batches)
        {
            var loadResult = await batch.LoadAsync(selection, cancellationToken);
            yield return new FileInspectorDeferredBatchResult(
                selection.RefreshVersion,
                batch.Category,
                batch.IsFinalBatch,
                loadResult.Updates,
                loadResult.ThumbnailBytes);
        }
    }
}
