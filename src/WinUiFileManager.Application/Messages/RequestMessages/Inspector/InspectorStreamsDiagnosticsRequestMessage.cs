namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer to enumerate a file's NTFS alternate data streams. Sent by
/// the inspector view model when a file is selected; the streams handler publishes an
/// <see cref="InspectorStreamsDiagnosticsResponseMessage"/>.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorStreamsDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
