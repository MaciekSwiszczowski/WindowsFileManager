namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer which processes/services hold a file open.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorLocksDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
