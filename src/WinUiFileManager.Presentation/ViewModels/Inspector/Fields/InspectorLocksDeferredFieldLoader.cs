using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorLocksDeferredFieldLoader : InspectorDeferredFieldLoaderBase<FileLockDiagnostics>
{
    private static readonly IReadOnlyList<string> LockFieldKeys =
    [
        "Is locked",
        "In Use",
        "Locked By",
        "Lock PIDs",
        "Lock Services",
    ];

    private readonly IMessenger _messenger;

    public InspectorLocksDeferredFieldLoader(IMessenger messenger)
    {
        _messenger = messenger;
    }

    protected override IReadOnlyList<string> FieldKeys => LockFieldKeys;

    protected override async Task<FileLockDiagnostics> LoadDiagnosticsAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var request = _messenger.Send(new InspectorLocksDiagnosticsRequestMessage(path, cancellationToken));
        return request.HasReceivedResponse
            ? await request.Response
            : FileLockDiagnostics.None;
    }

    protected override void Apply(FileLockDiagnostics diagnostics) =>
        FieldValueUpdater.ShowLockDiagnostics(diagnostics);
}
