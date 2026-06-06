using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Security category. Requests security-descriptor diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorSecurityDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
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
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorSecurityDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => SecurityFieldKeys;

    protected override Task ApplyAsync(FileSecurityDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowSecurityDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
