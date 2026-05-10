namespace WinUiFileManager.Application.Diagnostics;

public sealed record FileCloudDiagnosticsDetails(
    bool IsCloudControlled,
    string Status,
    string Provider,
    string SyncRoot,
    string SyncRootId,
    string ProviderId,
    string Available,
    string Transfer,
    string Custom)
{
    public static FileCloudDiagnosticsDetails None { get; } = new(
        false,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}
