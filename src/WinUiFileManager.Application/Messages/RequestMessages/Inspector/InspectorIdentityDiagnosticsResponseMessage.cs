namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Response message carrying NTFS identity diagnostics.</summary>
/// <param name="Diagnostics">Loaded diagnostics.</param>
public sealed record InspectorIdentityDiagnosticsResponseMessage(InspectorIdentityDiagnosticsDetails Diagnostics)
    : IInspectorDiagnosticsResponseMessage<InspectorIdentityDiagnosticsDetails>;
