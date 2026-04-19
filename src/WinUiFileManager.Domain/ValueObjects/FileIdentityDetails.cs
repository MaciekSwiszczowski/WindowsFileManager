namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileIdentityDetails(
    NtfsFileId FileId,
    string VolumeSerial,
    string LegacyFileIndex,
    string HardLinkCount,
    string FinalPath);
