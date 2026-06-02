namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying file-lock diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorLocksDiagnosticsResponseMessage(FileLockDiagnostics Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileLockDiagnostics>;
