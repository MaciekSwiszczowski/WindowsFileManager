using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages;

/// <summary>
/// Resolved request to delete selected items.
/// </summary>
public sealed record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<FileSystemEntryModel> Items) : IFileManagerMessengerMessage;
