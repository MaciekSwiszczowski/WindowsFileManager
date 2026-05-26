using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

public sealed class FileTableSelectionSnapshotRequestMessage : RequestMessage<bool>, IFileManagerMessengerMessage
{
    public FileTableSelectionSnapshotRequestMessage(NormalizedPath directoryPath, NormalizedPath oldPath, NormalizedPath newPath)
    {
        DirectoryPath = directoryPath;
        OldPath = oldPath;
        NewPath = newPath;
    }

    public NormalizedPath DirectoryPath { get; }

    public NormalizedPath OldPath { get; }

    public NormalizedPath NewPath { get; }
}
