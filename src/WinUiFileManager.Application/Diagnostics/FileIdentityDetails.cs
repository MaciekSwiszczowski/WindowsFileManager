namespace WinUiFileManager.Application.Diagnostics;

using WinUiFileManager.Application.FileEntries;

public sealed record FileIdentityDetails(
    NtfsFileId FileId,
    string VolumeSerial,
    string LegacyFileIndex,
    string HardLinkCount,
    string FinalPath);
