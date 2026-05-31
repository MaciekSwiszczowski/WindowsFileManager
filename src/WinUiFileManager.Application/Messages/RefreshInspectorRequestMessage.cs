using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Requests that the inspector reload its diagnostics from the current active-panel selection. Sent
/// after operations that may change a file's state; handled by the inspector view model in Presentation.
/// </summary>
public sealed record RefreshInspectorRequestMessage : IFileManagerMessengerMessage;
