using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Locks category. Requests file-lock / in-use diagnostics via
/// <see cref="InspectorLocksDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorLocksDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorLocksDiagnosticsRequestMessage,
        InspectorLocksDiagnosticsResponseMessage,
        FileLockDiagnostics>
{
    private static readonly IReadOnlyList<string> LockFieldKeys =
    [
        "Is locked",
        "In Use",
        "Locked By",
        "Lock PIDs",
        "Lock Services",
    ];

    public InspectorLocksDeferredFieldLoader(
        IMessenger messenger,
        ISchedulerProvider schedulers,
        ILogger<InspectorLocksDeferredFieldLoader> logger)
        : base(messenger, schedulers, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => LockFieldKeys;

    protected override InspectorLocksDiagnosticsRequestMessage CreateRequest(NormalizedPath path) => new(path);

    protected override Task ApplyAsync(FileLockDiagnostics diagnostics)
    {
        FieldValueUpdater.ShowLockDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
