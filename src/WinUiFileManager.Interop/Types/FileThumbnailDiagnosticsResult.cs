namespace WinUiFileManager.Interop.Types;

public sealed record FileThumbnailDiagnosticsResult(
    bool Success,
    byte[]? ThumbnailBytes,
    string? ProgId,
    string? ErrorMessage);
