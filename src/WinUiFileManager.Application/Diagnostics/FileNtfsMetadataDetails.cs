namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileNtfsMetadataDetails(
    FileAttributes Attributes,
    DateTime CreationTimeUtc,
    DateTime LastAccessTimeUtc,
    DateTime LastWriteTimeUtc,
    DateTime ChangeTimeUtc);
