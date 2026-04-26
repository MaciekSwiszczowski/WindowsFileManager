namespace WinUiFileManager.Presentation.ViewModels;

internal static partial class FilePaneViewModelLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Pane load canceled for {Path}")]
    public static partial void PaneLoadCanceled(ILogger logger, string path);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error, Message = "Failed to load directory {Path}")]
    public static partial void DirectoryLoadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Directory watcher pipeline failed for {Path}")]
    public static partial void DirectoryWatcherPipelineFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 103, Level = LogLevel.Debug, Message = "Failed to start directory watcher for {Path}")]
    public static partial void DirectoryWatcherStartFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "Directory watcher for {Path} requested a full rescan.")]
    public static partial void DirectoryWatcherRequestedFullRescan(ILogger logger, string path);

    [LoggerMessage(
        EventId = 105,
        Level = LogLevel.Information,
        Message = "Directory {MissingPath} no longer exists. Falling back to existing ancestor {ExistingPath}.")]
    public static partial void DirectoryFallbackToExistingAncestor(
        ILogger logger,
        string missingPath,
        string existingPath);
}
