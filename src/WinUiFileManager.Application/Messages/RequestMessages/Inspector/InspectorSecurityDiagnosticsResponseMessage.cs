namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying security-descriptor diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorSecurityDiagnosticsResponseMessage(FileSecurityDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileSecurityDiagnosticsDetails>;
