using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class LinksFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Links;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Links, "Link Target", "Target path of a symbolic link, junction, or shell shortcut.", 0),
        new(Links, "Link Status", "What kind of link Windows reports for the item.", 1),
        new(Links, "Reparse Tag", "Reparse point classification reported by Windows.", 2),
        new(Links, "Reparse Data", "Additional reparse data, when Windows can provide it.", 3),
        new(Links, "Object ID", "NTFS object identifier, when available.", 4),
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

            var diagnostics = await fileIdentityService.GetLinkDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Link Target", diagnostics.LinkTarget),
                new FileInspectorFieldUpdate("Link Status", diagnostics.LinkStatus),
                new FileInspectorFieldUpdate("Reparse Tag", diagnostics.ReparseTag),
                new FileInspectorFieldUpdate("Reparse Data", diagnostics.ReparseData),
                new FileInspectorFieldUpdate("Object ID", diagnostics.ObjectId)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load link diagnostics for {Path}", selection.FullPath);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Link Target", string.Empty),
                new FileInspectorFieldUpdate("Link Status", string.Empty),
                new FileInspectorFieldUpdate("Reparse Tag", string.Empty),
                new FileInspectorFieldUpdate("Reparse Data", string.Empty),
                new FileInspectorFieldUpdate("Object ID", string.Empty)
            ]);
        }
    }
}
