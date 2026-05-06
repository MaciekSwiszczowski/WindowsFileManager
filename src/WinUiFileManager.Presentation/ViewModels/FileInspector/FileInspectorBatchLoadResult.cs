namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed record FileInspectorBatchLoadResult(
    IReadOnlyList<FileInspectorFieldUpdate> Updates,
    byte[]? ThumbnailBytes = null);
