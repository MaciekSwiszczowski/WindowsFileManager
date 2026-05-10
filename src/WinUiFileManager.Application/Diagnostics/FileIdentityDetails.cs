namespace WinUiFileManager.Application.Diagnostics;

using Domain.ValueObjects;

public sealed record FileIdentityDetails(
    NtfsFileId FileId,
    string VolumeSerial,
    string LegacyFileIndex,
    string HardLinkCount,
    string FinalPath);
