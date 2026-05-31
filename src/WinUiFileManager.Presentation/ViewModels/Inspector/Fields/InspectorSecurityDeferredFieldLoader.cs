using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Security category. Requests security-descriptor diagnostics via
/// <see cref="InspectorSecurityDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
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

    protected override Task ApplyAsync(FileSecurityDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowSecurityDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
