namespace WinUiFileManager.Interop.Types;

public sealed record FileStreamDiagnosticsResult(
    bool Success,
    int AlternateStreamCount,
    IReadOnlyList<string> AlternateStreams,
    string? ErrorMessage);
