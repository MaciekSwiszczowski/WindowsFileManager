namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileThumbnailDiagnosticsDetails(
    byte[]? ThumbnailBytes,
    string ProgId);
