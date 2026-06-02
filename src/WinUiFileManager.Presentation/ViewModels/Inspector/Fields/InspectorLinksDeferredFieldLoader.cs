using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Links category. Requests link/reparse diagnostics via
/// <see cref="InspectorLinksDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorLinksDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorLinksDiagnosticsRequestMessage,
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

    public InspectorLinksDeferredFieldLoader(IMessenger messenger, ISchedulerProvider schedulers)
        : base(messenger, schedulers)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => LinkFieldKeys;

    protected override InspectorLinksDiagnosticsRequestMessage CreateRequest(NormalizedPath path) => new(path);

    protected override Task ApplyAsync(FileLinkDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowLinkDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
