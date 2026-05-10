namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileThumbnailDiagnosticsDetails(
    byte[]? ThumbnailBytes,
    string ProgId);
