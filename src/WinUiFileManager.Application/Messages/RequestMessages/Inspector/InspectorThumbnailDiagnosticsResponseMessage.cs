namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying shell-thumbnail diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorThumbnailDiagnosticsResponseMessage(FileThumbnailDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileThumbnailDiagnosticsDetails>;
