namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorDeferredBatchResult(
    long SelectionVersion,
    string Category,
    bool IsFinalBatch,
    IReadOnlyList<FileInspectorFieldUpdate> Updates);
