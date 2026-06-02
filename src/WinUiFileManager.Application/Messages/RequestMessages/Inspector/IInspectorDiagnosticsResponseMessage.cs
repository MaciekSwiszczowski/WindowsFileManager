using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Common contract for inspector diagnostics responses published by the Diagnostics layer.</summary>
/// <typeparam name="TDiagnostics">The diagnostics payload type.</typeparam>
public interface IInspectorDiagnosticsResponseMessage<out TDiagnostics> : IFileManagerMessengerMessage
{
    /// <summary>Loaded diagnostics payload.</summary>
    public TDiagnostics Diagnostics { get; }
}
