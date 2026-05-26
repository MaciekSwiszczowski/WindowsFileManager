using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

public sealed record SetFileAttributeFlagRequestedMessage(NormalizedPath Path, FileAttributes Flag, bool Enabled) : IFileManagerMessengerMessage;
