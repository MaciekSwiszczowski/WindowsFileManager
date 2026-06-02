namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer for a file's NTFS metadata and identity facts.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorIdentityDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
