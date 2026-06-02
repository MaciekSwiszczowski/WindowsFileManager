namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Response message carrying alternate-data-stream diagnostics for the inspector streams response channel.
/// </summary>
/// <param name="Diagnostics">Loaded streams diagnostics.</param>
public sealed record InspectorStreamsDiagnosticsResponseMessage(FileStreamDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileStreamDiagnosticsDetails>;
