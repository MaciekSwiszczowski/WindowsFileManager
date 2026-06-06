using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Ids category and authoritative NTFS timestamps. Requests identity diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/> and applies them; it overwrites the fast timestamps
/// populated immediately by the basic fields with the precise NTFS values.
/// </summary>
internal sealed class InspectorIdentityDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorIdentityDiagnosticsResponseMessage,
        InspectorIdentityDiagnosticsDetails>
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

    public InspectorIdentityDeferredFieldLoader(
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorIdentityDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => IdentityFieldKeys;

    protected override Task ApplyAsync(InspectorIdentityDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowIdentityDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
