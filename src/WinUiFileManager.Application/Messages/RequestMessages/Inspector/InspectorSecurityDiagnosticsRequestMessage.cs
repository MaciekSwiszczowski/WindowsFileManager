namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer for a file's security-descriptor summary.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorSecurityDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
