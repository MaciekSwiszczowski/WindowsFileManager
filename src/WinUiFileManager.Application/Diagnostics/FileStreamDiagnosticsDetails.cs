namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileStreamDiagnosticsDetails(
    string AlternateStreamCount,
    IReadOnlyList<string> AlternateStreams);
