namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying link/reparse diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorLinksDiagnosticsResponseMessage(FileLinkDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<FileLinkDiagnosticsDetails>;
