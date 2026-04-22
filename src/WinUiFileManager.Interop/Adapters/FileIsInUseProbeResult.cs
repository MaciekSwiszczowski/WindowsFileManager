namespace WinUiFileManager.Interop.Adapters;

internal sealed record FileIsInUseProbeResult(
    int HResult,
    string? AppName,
    string? Usage,
    bool? CanSwitchTo,
    bool? CanClose,
    string? ErrorMessage);
