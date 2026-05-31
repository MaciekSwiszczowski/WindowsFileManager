namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileLinkDiagnosticsDetails(
    string LinkTarget,
    string LinkStatus,
    string ReparseTag,
    string ReparseData,
    string ObjectId)
{
    public static FileLinkDiagnosticsDetails Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
