namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileThumbnailDiagnosticsDetails(
    byte[]? ThumbnailBytes,
    string ProgId)
{
    public static FileThumbnailDiagnosticsDetails Empty { get; } = new(null, string.Empty);
}
