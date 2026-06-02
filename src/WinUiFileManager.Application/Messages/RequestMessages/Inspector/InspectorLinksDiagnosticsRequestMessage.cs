namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer for a file's link/reparse-point details.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorLinksDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
