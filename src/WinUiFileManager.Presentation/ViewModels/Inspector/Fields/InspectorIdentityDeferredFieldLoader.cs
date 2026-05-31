using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Ids category and authoritative NTFS timestamps. Requests identity diagnostics via
/// <see cref="InspectorIdentityDiagnosticsRequestMessage"/> and applies them; it overwrites the fast timestamps
/// populated immediately by the basic fields with the precise NTFS values.
/// </summary>
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

    protected override Task ApplyAsync(InspectorIdentityDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowIdentityDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
