using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Deferred loader for the Cloud category. Requests cloud/placeholder (sync-root) diagnostics via
/// <see cref="InspectorDiagnosticsRequestMessage"/> and applies them through the field-value updater.
/// </summary>
internal sealed class InspectorCloudDeferredFieldLoader :
    InspectorDeferredFieldLoaderBase<
        InspectorCloudDiagnosticsResponseMessage,
        FileCloudDiagnosticsDetails>
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

    public InspectorCloudDeferredFieldLoader(
        IFileManagerMessenger messenger,
        SynchronizationContext uiSynchronizationContext,
        ILogger<InspectorCloudDeferredFieldLoader> logger)
        : base(messenger, uiSynchronizationContext, logger)
    {
    }

    protected override IReadOnlyList<string> FieldKeys => CloudFieldKeys;

    protected override Task ApplyAsync(FileCloudDiagnosticsDetails diagnostics)
    {
        FieldValueUpdater.ShowCloudDiagnostics(diagnostics);
        return Task.CompletedTask;
    }
}
