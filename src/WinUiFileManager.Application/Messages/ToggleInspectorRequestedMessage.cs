using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages;

public sealed record ToggleInspectorRequestedMessage(bool IsVisible) : IFileManagerMessengerMessage;
