namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable link/reparse-point facts for a file (symlink/junction target, reparse tag and data,
/// object id), shown in the inspector's Links section. Produced by the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// All fields are pre-formatted strings.
/// </summary>
public sealed record FileLinkDiagnosticsDetails(
    string LinkTarget,
    string LinkStatus,
    string ReparseTag,
    string ReparseData,
    string ObjectId)
{
    /// <summary>Sentinel for a plain file with no reparse/link data (all fields empty).</summary>
    public static FileLinkDiagnosticsDetails Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
