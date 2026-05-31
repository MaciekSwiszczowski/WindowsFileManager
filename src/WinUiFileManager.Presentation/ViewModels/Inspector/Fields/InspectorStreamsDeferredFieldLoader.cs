using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorStreamsDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileStreamDiagnosticsDetails>
{
    private readonly IMessenger _messenger;

    public InspectorStreamsDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override async Task<FileStreamDiagnosticsDetails> LoadDiagnosticsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorStreamsDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileStreamDiagnosticsDetails.Empty;
    }

    protected override void Apply(FileStreamDiagnosticsDetails diagnostics) =>
        FieldValueUpdater.ShowStreamsDiagnostics(diagnostics);
}
