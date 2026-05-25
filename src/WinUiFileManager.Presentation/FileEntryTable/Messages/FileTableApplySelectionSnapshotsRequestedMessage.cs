namespace WinUiFileManager.Presentation.FileEntryTable.Messages;

public sealed record FileTableApplySelectionSnapshotsRequestedMessage(
    NormalizedPath DirectoryPath,
    NormalizedPath? OldPath,
    NormalizedPath? NewPath) : IFileManagerMessengerMessage;
