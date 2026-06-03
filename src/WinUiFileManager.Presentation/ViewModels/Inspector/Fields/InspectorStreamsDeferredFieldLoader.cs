using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Streams category. Requests alternate-data-stream diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorStreamsDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorStreamsDiagnosticsResponseMessage,
        FileStreamDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> StreamFieldKeys =
    [
        "Alternate Stream Count",
        "Alternate Streams",
    ];

    public InspectorStreamsDeferredFieldLoader(
        IMessenger messenger,
        ISchedulerProvider schedulers,
        ILogger<InspectorStreamsDeferredFieldLoader> logger)
        : base(messenger, schedulers, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => StreamFieldKeys;

    protected override Task ApplyAsync(FileStreamDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowStreamsDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
