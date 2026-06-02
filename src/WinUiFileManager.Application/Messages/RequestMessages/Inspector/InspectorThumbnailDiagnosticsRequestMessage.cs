namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer for a file's shell thumbnail.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorThumbnailDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
