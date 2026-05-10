namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileLinkDiagnosticsDetails(
    string LinkTarget,
    string LinkStatus,
    string ReparseTag,
    string ReparseData,
    string ObjectId);
