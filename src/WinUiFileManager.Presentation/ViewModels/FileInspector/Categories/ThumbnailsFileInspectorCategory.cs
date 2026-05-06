using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class ThumbnailsFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Thumbnails;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Thumbnails, "Thumbnail", "Thumbnail preview reported by Windows, when available.", 0),
        new(Thumbnails, "Has Thumbnail", "Whether Windows could provide a thumbnail for the selected item.", 1),
        new(Thumbnails, "Association", "Shell association or file type hint used for the thumbnail, when available.", 2)
    ];

    public static async Task<FileInspectorBatchLoadResult> LoadAsync(
        IFileIdentityService fileIdentityService,
        ILogger<FileInspectorViewModel> logger,
        FileInspectorSelection selection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var diagnostics = await fileIdentityService.GetThumbnailDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Has Thumbnail", diagnostics.ThumbnailBytes is { Length: > 0 } ? "Yes" : "No"),
                new FileInspectorFieldUpdate("Association", diagnostics.ProgId)
            ], diagnostics.ThumbnailBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load thumbnail diagnostics for {Path}", selection.FullPath);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Has Thumbnail", "No"),
                new FileInspectorFieldUpdate("Association", string.Empty)
            ]);
        }
    }
}
