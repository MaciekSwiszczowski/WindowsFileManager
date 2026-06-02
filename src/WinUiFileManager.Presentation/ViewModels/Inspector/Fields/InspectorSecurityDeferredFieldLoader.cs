using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Security category. Requests security-descriptor diagnostics via
/// <see cref="InspectorSecurityDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorSecurityDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorSecurityDiagnosticsRequestMessage,
        InspectorSecurityDiagnosticsResponseMessage,
        FileSecurityDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> SecurityFieldKeys =
    [
        "Owner",
        "Group",
        "DACL Summary",
        "SACL Summary",
        "Inherited",
        "Protected",
    ];

    public InspectorSecurityDeferredFieldLoader(
        IMessenger messenger,
        ISchedulerProvider schedulers,
        ILogger<InspectorSecurityDeferredFieldLoader> logger)
        : base(messenger, schedulers, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => SecurityFieldKeys;

    protected override InspectorSecurityDiagnosticsRequestMessage CreateRequest(NormalizedPath path) => new(path);

    protected override Task ApplyAsync(FileSecurityDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowSecurityDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
