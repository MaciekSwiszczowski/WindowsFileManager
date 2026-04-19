namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorDeferredBatchResult(
    string Category,
    bool IsFinalBatch,
    IReadOnlyList<FileInspectorFieldUpdate> Updates);
