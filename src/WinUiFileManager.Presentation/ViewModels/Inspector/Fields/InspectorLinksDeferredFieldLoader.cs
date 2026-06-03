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
        IMessenger messenger,
        ISchedulerProvider schedulers,
        ILogger<InspectorLinksDeferredFieldLoader> logger)
        : base(messenger, schedulers, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => LinkFieldKeys;

    protected override Task ApplyAsync(FileLinkDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowLinkDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
