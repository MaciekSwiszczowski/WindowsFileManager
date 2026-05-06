using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class SecurityFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Security;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Security, "Owner", "Owner of the file or folder.", 0),
        new(Security, "Group", "Primary group of the file or folder.", 1),
        new(Security, "DACL Summary", "Summary of access rules from the discretionary access control list.", 2),
        new(Security, "SACL Summary", "Summary of audit rules from the system access control list.", 3),
        new(Security, "Inherited", "Whether the permissions are inherited.", 4),
        new(Security, "Protected", "Whether inherited permissions are blocked.", 5)
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

            var diagnostics = await fileIdentityService.GetSecurityDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Owner", diagnostics.Owner),
                new FileInspectorFieldUpdate("Group", diagnostics.Group),
                new FileInspectorFieldUpdate("DACL Summary", diagnostics.DaclSummary),
                new FileInspectorFieldUpdate("SACL Summary", diagnostics.SaclSummary),
                new FileInspectorFieldUpdate("Inherited", FileInspectorFormatting.OptionalBoolean(diagnostics.Inherited)),
                new FileInspectorFieldUpdate("Protected", FileInspectorFormatting.OptionalBoolean(diagnostics.Protected))
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load security diagnostics for {Path}", selection.FullPath);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Owner", string.Empty),
                new FileInspectorFieldUpdate("Group", string.Empty),
                new FileInspectorFieldUpdate("DACL Summary", string.Empty),
                new FileInspectorFieldUpdate("SACL Summary", string.Empty),
                new FileInspectorFieldUpdate("Inherited", string.Empty),
                new FileInspectorFieldUpdate("Protected", string.Empty)
            ]);
        }
    }
}
