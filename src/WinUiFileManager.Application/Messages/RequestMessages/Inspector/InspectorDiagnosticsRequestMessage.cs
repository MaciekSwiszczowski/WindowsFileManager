using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Requests all inspector diagnostics for one selected file-system path. Every inspector diagnostics handler
/// receives this message and publishes its own category-specific response.
/// </summary>
public sealed record InspectorDiagnosticsRequestMessage(NormalizedPath Path) : IFileManagerMessengerMessage;
