namespace WinUiFileManager.Application.Diagnostics;

using WinUiFileManager.Application.FileEntries;

/// <summary>
/// Immutable NTFS identity facts for a file (file id, volume serial, hard-link count, resolved final
/// path), shown in the inspector's identity section. String fields are pre-formatted for display.
/// </summary>
/// <param name="FileId">The NTFS file identifier; <see cref="NtfsFileId.None"/> when unavailable.</param>
/// <param name="VolumeSerial">The hosting volume's serial number, formatted for display.</param>
/// <param name="LegacyFileIndex">The legacy 64-bit file index, formatted for display.</param>
/// <param name="HardLinkCount">Number of hard links to the file, formatted for display.</param>
/// <param name="FinalPath">The fully-resolved final path (links/junctions followed).</param>
public sealed record FileIdentityDetails(
    NtfsFileId FileId,
    string VolumeSerial,
    string LegacyFileIndex,
    string HardLinkCount,
    string FinalPath);
