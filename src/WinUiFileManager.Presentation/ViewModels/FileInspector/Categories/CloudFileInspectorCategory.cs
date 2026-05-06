using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class CloudFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Cloud;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Cloud, "Status", "Combined cloud-file state summary such as hydrated, dehydrated, pinned, synced, or uploading.", 0),
        new(Cloud, "Provider", "Cloud provider display name.", 1),
        new(Cloud, "Sync Root", "Owning sync-root path or display name.", 2),
        new(Cloud, "Root ID", "Sync-root registration identifier.", 3),
        new(Cloud, "Provider ID", "Provider identifier from the sync-root registration.", 4),
        new(Cloud, "Available", "Whether the selected item is currently available locally.", 5),
        new(Cloud, "Transfer", "Current transfer state such as upload, download, or paused, when Windows exposes it.", 6),
        new(Cloud, "Custom", "Provider-defined custom cloud status text, when available.", 7)
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

            var diagnostics = await fileIdentityService.GetCloudDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            if (!diagnostics.IsCloudControlled)
            {
                return CreateEmptyResult();
            }

            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Status", diagnostics.Status),
                new FileInspectorFieldUpdate("Provider", diagnostics.Provider),
                new FileInspectorFieldUpdate("Sync Root", diagnostics.SyncRoot),
                new FileInspectorFieldUpdate("Root ID", diagnostics.SyncRootId),
                new FileInspectorFieldUpdate("Provider ID", diagnostics.ProviderId),
                new FileInspectorFieldUpdate("Available", diagnostics.Available),
                new FileInspectorFieldUpdate("Transfer", diagnostics.Transfer),
                new FileInspectorFieldUpdate("Custom", diagnostics.Custom)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load cloud diagnostics for {Path}", selection.FullPath);
            return CreateEmptyResult();
        }
    }

    private static FileInspectorBatchLoadResult CreateEmptyResult() =>
        new(
        [
            new FileInspectorFieldUpdate("Status", string.Empty),
            new FileInspectorFieldUpdate("Provider", string.Empty),
            new FileInspectorFieldUpdate("Sync Root", string.Empty),
            new FileInspectorFieldUpdate("Root ID", string.Empty),
            new FileInspectorFieldUpdate("Provider ID", string.Empty),
            new FileInspectorFieldUpdate("Available", string.Empty),
            new FileInspectorFieldUpdate("Transfer", string.Empty),
            new FileInspectorFieldUpdate("Custom", string.Empty)
        ]);
}
