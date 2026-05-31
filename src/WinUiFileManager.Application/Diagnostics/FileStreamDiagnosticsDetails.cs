namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileStreamDiagnosticsDetails(string AlternateStreamCount, IReadOnlyList<string> AlternateStreams)
{
    public static FileStreamDiagnosticsDetails Empty { get; } = new("0", []);
}
