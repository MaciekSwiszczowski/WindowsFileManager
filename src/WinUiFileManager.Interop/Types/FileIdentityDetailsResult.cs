namespace WinUiFileManager.Interop.Types;

public sealed record FileIdentityDetailsResult(
    bool Success,
    byte[]? FileId128,
    uint? VolumeSerialNumber,
    ulong? LegacyFileIndex,
    uint? HardLinkCount,
    string? FinalPath,
    string? ErrorMessage);
