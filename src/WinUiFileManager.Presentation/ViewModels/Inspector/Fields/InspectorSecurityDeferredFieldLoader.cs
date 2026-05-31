using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorSecurityDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileSecurityDiagnosticsDetails>
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

    private readonly IMessenger _messenger;

    public InspectorSecurityDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => SecurityFieldKeys;

    protected override async Task<FileSecurityDiagnosticsDetails> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorSecurityDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileSecurityDiagnosticsDetails.Empty;
    }

    protected override void Apply(FileSecurityDiagnosticsDetails diagnostics) =>
        FieldValueUpdater.ShowSecurityDiagnostics(diagnostics);
}
