using Microsoft.Extensions.Logging;

namespace WinUiFileManager.Infrastructure.FileSystem;

internal static partial class WindowsFileSystemServiceLog
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "Directory does not exist: {Path}")]
    public static partial void DirectoryDoesNotExist(ILogger logger, string path);
}
