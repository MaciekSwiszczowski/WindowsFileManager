using Microsoft.Extensions.Logging;
using WinUiFileManager.Domain.Enums;

namespace WinUiFileManager.Infrastructure.Execution;

internal static partial class WindowsFileOperationServiceLog
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Warning, Message = "Destination directory does not exist: {Path}")]
    public static partial void DestinationDirectoryMissing(ILogger logger, string path);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Warning,
        Message = "Operation {OperationType} cancelled after {Completed}/{Total} items")]
    public static partial void OperationCancelled(
        ILogger logger,
        OperationType operationType,
        int completed,
        int total);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Error,
        Message = "Operation {Type} failed for {Path}: error {ErrorCode} - {Message}")]
    public static partial void OperationFailed(
        ILogger logger,
        OperationType type,
        string path,
        int errorCode,
        string message);

    [LoggerMessage(EventId = 303, Level = LogLevel.Warning, Message = "Failed to remove source directory after move: {Path}")]
    public static partial void MoveSourceCleanupFailed(ILogger logger, Exception exception, string path);
}
