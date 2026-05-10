namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileSecurityDiagnosticsDetails(
    string Owner,
    string Group,
    string DaclSummary,
    string SaclSummary,
    bool? Inherited,
    bool? Protected);
