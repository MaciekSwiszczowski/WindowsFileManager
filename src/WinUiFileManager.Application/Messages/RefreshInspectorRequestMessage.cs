using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

/// <summary>
/// Requests refreshing the inspector from the current selection of a panel.
/// </summary>
public sealed record RefreshInspectorRequestMessage(string Identity) : IFileManagerMessengerMessage;
