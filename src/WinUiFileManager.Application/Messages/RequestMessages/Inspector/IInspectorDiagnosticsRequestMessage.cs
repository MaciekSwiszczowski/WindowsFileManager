using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>Common contract for inspector diagnostics requests sent to the Diagnostics layer.</summary>
public interface IInspectorDiagnosticsRequestMessage : IFileManagerMessengerMessage
{
    /// <summary>The file system entry being inspected.</summary>
    public NormalizedPath Path { get; }
}
