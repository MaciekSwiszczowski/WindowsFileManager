namespace WinUiFileManager.Presentation.ViewModels;

public sealed record FileInspectorDeferredBatchResult(
    long SelectionVersion,
    FileInspectorCategory Category,
    bool IsFinalBatch,
    IReadOnlyList<FileInspectorFieldUpdate> Updates,
    byte[]? ThumbnailBytes = null);
