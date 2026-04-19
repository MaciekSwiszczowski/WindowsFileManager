namespace WinUiFileManager.Domain.ValueObjects;

public sealed record FileSecurityDiagnosticsDetails(
    string Owner,
    string Group,
    string DaclSummary,
    string SaclSummary,
    bool? Inherited,
    bool? Protected);
