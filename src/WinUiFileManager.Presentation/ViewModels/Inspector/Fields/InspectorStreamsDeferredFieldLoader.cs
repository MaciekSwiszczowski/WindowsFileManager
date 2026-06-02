using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Streams category. Requests alternate-data-stream diagnostics via
/// <see cref="InspectorStreamsDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorStreamsDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorStreamsDiagnosticsRequestMessage,
        InspectorStreamsDiagnosticsResponseMessage,
        FileStreamDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> StreamFieldKeys =
    [
        "Alternate Stream Count",
        "Alternate Streams",
    ];

    public InspectorStreamsDeferredFieldLoader(IMessenger messenger, ISchedulerProvider schedulers)
        : base(messenger, schedulers)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => StreamFieldKeys;

    protected override InspectorStreamsDiagnosticsRequestMessage CreateRequest(NormalizedPath path) => new(path);

    protected override Task ApplyAsync(FileStreamDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowStreamsDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
