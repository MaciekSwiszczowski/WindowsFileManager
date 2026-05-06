namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed record FileInspectorDeferredBatchDefinition(
    FileInspectorCategory Category,
    bool IsFinalBatch,
    Func<FileInspectorSelection, CancellationToken, Task<FileInspectorBatchLoadResult>> LoadAsync);
