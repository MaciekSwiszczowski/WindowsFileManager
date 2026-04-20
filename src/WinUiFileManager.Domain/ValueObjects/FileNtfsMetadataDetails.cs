namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileNtfsMetadataDetails(
    FileAttributes Attributes,
    DateTime CreationTimeUtc,
    DateTime LastAccessTimeUtc,
    DateTime LastWriteTimeUtc,
    DateTime ChangeTimeUtc);
