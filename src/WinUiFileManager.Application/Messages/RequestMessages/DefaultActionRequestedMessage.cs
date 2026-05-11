using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to open a file or enter a directory.
/// </summary>
public sealed record DefaultActionRequestedMessage(
    string SourceIdentity,
    FileSystemEntryModel Item) : IFileManagerMessengerMessage;
