using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class LocksFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Locks;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Locks, "Is locked", "Whether the selected item appears to be locked based on the other lock diagnostics in this category.", 0),
        new(Locks, "In Use", "Whether Windows currently reports the item as in use. Best-effort diagnostic.", 1),
        new(Locks, "Locked By", "Applications or services that Windows reports as using this item.", 2),
        new(Locks, "Lock PIDs", "Process IDs of applications using this item. Useful in Task Manager or Process Explorer.", 3),
        new(Locks, "Lock Services", "Service names associated with the lock, when available.", 4)
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

            var diagnostics = await fileIdentityService.GetLockDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            if (!HasPositiveEvidence(diagnostics))
            {
                return CreateUnlockedResult();
            }

            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Is locked", "True"),
                new FileInspectorFieldUpdate("In Use", FileInspectorFormatting.OptionalBoolean(diagnostics.InUse)),
                new FileInspectorFieldUpdate(
                    "Locked By",
                    diagnostics.LockBy.Count == 0 ? string.Empty : string.Join(Environment.NewLine, diagnostics.LockBy)),
                new FileInspectorFieldUpdate(
                    "Lock PIDs",
                    diagnostics.LockPids.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockPids)),
                new FileInspectorFieldUpdate(
                    "Lock Services",
                    diagnostics.LockServices.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockServices))
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load lock diagnostics for {Path}", selection.FullPath);
            return CreateUnlockedResult();
        }
    }

    private static bool HasPositiveEvidence(FileLockDiagnostics diagnostics) =>
        diagnostics.InUse == true
        || diagnostics.LockBy.Count > 0
        || diagnostics.LockPids.Count > 0
        || diagnostics.LockServices.Count > 0;

    private static FileInspectorBatchLoadResult CreateUnlockedResult() =>
        new(
        [
            new FileInspectorFieldUpdate("Is locked", "False"),
            new FileInspectorFieldUpdate("In Use", string.Empty),
            new FileInspectorFieldUpdate("Locked By", string.Empty),
            new FileInspectorFieldUpdate("Lock PIDs", string.Empty),
            new FileInspectorFieldUpdate("Lock Services", string.Empty)
        ]);
}
