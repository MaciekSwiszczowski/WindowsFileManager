using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable result describing a file's cloud/placeholder (OneDrive-style Cloud Files API) state, as
/// surfaced in the inspector's Cloud section. Produced by the Diagnostics layer in reply to
/// <see cref="InspectorDiagnosticsRequestMessage"/>.
/// </summary>
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
    /// <summary>Sentinel for a file with no cloud involvement (all fields empty, <c>IsCloudControlled = false</c>).</summary>
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
