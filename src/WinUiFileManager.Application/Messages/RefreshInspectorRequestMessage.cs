using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Requests refreshing the inspector from the current active panel selection.
/// </summary>
public sealed record RefreshInspectorRequestMessage : IFileManagerMessengerMessage;
