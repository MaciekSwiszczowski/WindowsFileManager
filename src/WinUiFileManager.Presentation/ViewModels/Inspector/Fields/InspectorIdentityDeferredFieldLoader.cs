using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorIdentityDeferredFieldLoader : InspectorDeferredFieldLoaderBase<InspectorIdentityDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> IdentityFieldKeys =
    [
        "Created",
        "Accessed",
        "Modified",
        "MFT Changed",
        "File ID",
        "Volume Serial",
        "File Index (64-bit)",
        "Hard Link Count",
        "Final Path",
    ];

    private readonly IMessenger _messenger;

    public InspectorIdentityDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => IdentityFieldKeys;

    protected override async Task<InspectorIdentityDiagnosticsDetails> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorIdentityDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : InspectorIdentityDiagnosticsDetails.Empty;
    }

    protected override void Apply(InspectorIdentityDiagnosticsDetails diagnostics) =>
        FieldValueUpdater.ShowIdentityDiagnostics(diagnostics);
}
