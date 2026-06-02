namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Message asking the Diagnostics layer to compute a file's cloud/placeholder state.
/// </summary>
/// <param name="Path">The file to inspect.</param>
public sealed record InspectorCloudDiagnosticsRequestMessage(NormalizedPath Path) : IInspectorDiagnosticsRequestMessage;
