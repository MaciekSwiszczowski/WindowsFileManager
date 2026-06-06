using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Links category. Requests link/reparse diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorLinksDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorLinksDiagnosticsResponseMessage,
        FileLinkDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> LinkFieldKeys =
    [
        "Link Target",
        "Link Status",
        "Reparse Tag",
        "Reparse Data",
        "Object ID",
    ];

    public InspectorLinksDeferredFieldLoader(
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorLinksDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => LinkFieldKeys;

    protected override Task ApplyAsync(FileLinkDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowLinkDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
