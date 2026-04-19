namespace WinUiFileManager.Interop.Types;

public sealed record FileSecurityDiagnosticsResult(
    bool Success,
    string? Owner,
    string? Group,
    string? DaclSummary,
    string? SaclSummary,
    bool? Inherited,
    bool? Protected,
    string? ErrorMessage);
