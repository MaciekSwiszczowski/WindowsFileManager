namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable security-descriptor summary for a file (owner, group, DACL/SACL summaries, inheritance
/// flags), shown in the inspector's Security section. Produced by the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorSecurityDiagnosticsRequestMessage"/>.
/// </summary>
/// <param name="Owner">Owner principal, formatted for display.</param>
/// <param name="Group">Primary group principal, formatted for display.</param>
/// <param name="DaclSummary">Human-readable summary of the discretionary ACL.</param>
/// <param name="SaclSummary">Human-readable summary of the system (audit) ACL.</param>
/// <param name="Inherited">Whether ACEs are inherited; <see langword="null"/> when unknown.</param>
/// <param name="Protected">Whether the DACL is protected from inheritance; <see langword="null"/> when unknown.</param>
public sealed record FileSecurityDiagnosticsDetails(
    string Owner,
    string Group,
    string DaclSummary,
    string SaclSummary,
    bool? Inherited,
    bool? Protected)
{
    /// <summary>Sentinel for "no security information available" (empty strings, null flags).</summary>
    public static FileSecurityDiagnosticsDetails Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, null, null);
}
