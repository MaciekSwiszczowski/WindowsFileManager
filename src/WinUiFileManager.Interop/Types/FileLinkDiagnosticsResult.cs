namespace WinUiFileManager.Interop.Types;

public sealed record FileLinkDiagnosticsResult(
    bool Success,
    string? LinkTarget,
    string? LinkStatus,
    string? ReparseTag,
    string? ReparseData,
    string? ObjectId,
    string? ErrorMessage);
