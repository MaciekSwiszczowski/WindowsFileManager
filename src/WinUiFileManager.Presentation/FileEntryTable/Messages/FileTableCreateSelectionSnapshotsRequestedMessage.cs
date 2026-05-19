using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record FileTableCreateSelectionSnapshotsRequestedMessage(
    NormalizedPath DirectoryPath) : IFileManagerMessengerMessage;
