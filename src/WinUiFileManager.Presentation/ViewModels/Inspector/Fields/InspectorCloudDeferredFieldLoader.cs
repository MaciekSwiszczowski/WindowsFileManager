using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorCloudDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileCloudDiagnosticsDetails>
{
    private static readonly IReadOnlyList<string> CloudFieldKeys =
    [
        "Status",
        "Provider",
        "Sync Root",
        "Root ID",
        "Provider ID",
        "Available",
        "Transfer",
        "Custom",
    ];

    private readonly IMessenger _messenger;

    public InspectorCloudDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => CloudFieldKeys;

    protected override async Task<FileCloudDiagnosticsDetails> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorCloudDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileCloudDiagnosticsDetails.None;
    }

    protected override Task ApplyAsync(FileCloudDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowCloudDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
