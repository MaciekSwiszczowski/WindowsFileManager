namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable raw NTFS metadata for a file (attribute flags and the four NTFS timestamps, including the
/// change/MFT-record-change time not exposed by the BCL), shown in the inspector's identity section.
/// All times are UTC.
/// </summary>
public sealed record FileNtfsMetadataDetails(
    FileAttributes Attributes,
    DateTime CreationTimeUtc,
    DateTime LastAccessTimeUtc,
    DateTime LastWriteTimeUtc,
    DateTime ChangeTimeUtc);
