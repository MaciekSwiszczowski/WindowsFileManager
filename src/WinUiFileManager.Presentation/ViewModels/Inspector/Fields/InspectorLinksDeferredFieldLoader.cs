using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorLinksDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileLinkDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> LinkFieldKeys =
    [
        "Link Target",
        "Link Status",
        "Reparse Tag",
        "Reparse Data",
        "Object ID",
    ];

    private readonly IMessenger _messenger;

    public InspectorLinksDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => LinkFieldKeys;

    protected override async Task<FileLinkDiagnosticsDetails> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorLinksDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileLinkDiagnosticsDetails.Empty;
    }

    protected override Task ApplyAsync(FileLinkDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowLinkDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
