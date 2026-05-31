using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Locks category. Requests file-lock / in-use diagnostics via
/// <see cref="InspectorLocksDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
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

    protected override Task ApplyAsync(FileLockDiagnostics diagnostics)
    {
        FieldValueUpdater.ShowLockDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
