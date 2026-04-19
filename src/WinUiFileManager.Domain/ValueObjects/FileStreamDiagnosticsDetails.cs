namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileStreamDiagnosticsDetails(
    string AlternateStreamCount,
    IReadOnlyList<string> AlternateStreams);
