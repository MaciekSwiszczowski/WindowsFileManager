using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

public sealed record PropertiesRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item) : IFileManagerMessengerMessage;
