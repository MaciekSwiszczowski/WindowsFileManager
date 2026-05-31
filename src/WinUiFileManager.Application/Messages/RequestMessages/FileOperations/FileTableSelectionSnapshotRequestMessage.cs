using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

/// <summary>
/// Request sent before a file operation that may replace a row in the file table.
/// </summary>
/// <remarks>
/// The active table should capture its current selection and active item, then watch its items collection
/// for <see cref="NewPath"/>. When the replacement row appears, the table restores the captured state.
/// The response is <c>true</c> when a table accepted the snapshot request; otherwise <c>false</c>.
/// </remarks>
public sealed class FileTableSelectionSnapshotRequestMessage : RequestMessage<bool>, IFileManagerMessengerMessage
{
    public FileTableSelectionSnapshotRequestMessage(NormalizedPath directoryPath, NormalizedPath oldPath, NormalizedPath newPath)
    {
        DirectoryPath = directoryPath;
        OldPath = oldPath;
        NewPath = newPath;
    }

    /// <summary>
    /// Directory displayed by the table that should capture the snapshot.
    /// </summary>
    public NormalizedPath DirectoryPath { get; }

    /// <summary>
    /// Path of the item before the operation. The table uses its file name to decide whether the snapshot is relevant.
    /// </summary>
    public NormalizedPath OldPath { get; }

    /// <summary>
    /// Path of the item after the operation. The table waits for a row with this file name before restoring.
    /// </summary>
    public NormalizedPath NewPath { get; }

    /// <summary>
    /// Replies once, guarding against a double reply. Multiple tables may receive this request; only the
    /// first matching table should answer, and <see cref="RequestMessage{T}.Reply"/> throws if called twice.
    /// </summary>
    /// <param name="response"><see langword="true"/> if this table accepted the snapshot request.</param>
    public void TryReply(bool response)
    {
        if (!HasReceivedResponse)
        {
            Reply(response);
        }
    }
}
