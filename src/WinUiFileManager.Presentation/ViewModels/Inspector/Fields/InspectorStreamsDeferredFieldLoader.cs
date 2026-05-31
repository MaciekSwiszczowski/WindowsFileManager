using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorStreamsDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileStreamDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> StreamFieldKeys =
    [
        "Alternate Stream Count",
        "Alternate Streams",
    ];

    private readonly IMessenger _messenger;

    public InspectorStreamsDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => StreamFieldKeys;

    protected override async Task<FileStreamDiagnosticsDetails> LoadDiagnosticsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorStreamsDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileStreamDiagnosticsDetails.Empty;
    }

    protected override Task ApplyAsync(FileStreamDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowStreamsDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
