namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying cloud/placeholder diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorCloudDiagnosticsResponseMessage(FileCloudDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileCloudDiagnosticsDetails>;
