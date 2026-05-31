namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileSecurityDiagnosticsDetails(
    string Owner,
    string Group,
    string DaclSummary,
    string SaclSummary,
    bool? Inherited,
    bool? Protected)
{
    public static FileSecurityDiagnosticsDetails Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, null, null);
}
