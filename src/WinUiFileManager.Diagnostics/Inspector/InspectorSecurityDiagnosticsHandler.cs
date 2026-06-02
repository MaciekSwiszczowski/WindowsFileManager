using System.Security.AccessControl;
using System.Security.Principal;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorSecurityDiagnosticsRequestMessage"/> with a
/// summary of a path's security descriptor: owner, group, DACL/SACL counts, and inheritance state.
/// </summary>
/// <remarks>
/// SACL/owner reads can throw on privilege/access issues, so they are wrapped in best-effort try/catch helpers
/// that degrade to empty strings rather than failing the whole load.
/// </remarks>
public sealed class InspectorSecurityDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        InspectorSecurityDiagnosticsRequestMessage,
        FileSecurityDiagnosticsDetails,
        InspectorSecurityDiagnosticsResponseMessage>
{
    public InspectorSecurityDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorSecurityDiagnosticsHandler> logger)
        : base(messenger, logger)
    {
    }

    /// <summary>
    /// Reads and summarizes the security descriptor for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>Security summary, or <see cref="FileSecurityDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to empty by the base class.</remarks>
    protected override Task<FileSecurityDiagnosticsDetails> LoadAsync(
        InspectorSecurityDiagnosticsRequestMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = message.Path.DisplayPath;
        FileSystemSecurity security = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl()
            : new FileInfo(path).GetAccessControl();

        var owner = SafeIdentityToString(() => security.GetOwner(typeof(NTAccount)));
        var group = SafeIdentityToString(() => security.GetGroup(typeof(NTAccount)));
        var daclSummary = SummarizeAccessRules(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        var saclSummary = TrySummarizeAuditRules(security);

        return Task.FromResult(new FileSecurityDiagnosticsDetails(
            owner,
            group,
            daclSummary,
            saclSummary,
            !security.AreAccessRulesProtected,
            security.AreAccessRulesProtected));
    }

    protected override InspectorSecurityDiagnosticsResponseMessage CreateResponse(FileSecurityDiagnosticsDetails diagnostics) =>
        new(diagnostics);

    protected override FileSecurityDiagnosticsDetails GetEmptyDiagnostics(InspectorSecurityDiagnosticsRequestMessage request) =>
        FileSecurityDiagnosticsDetails.Empty;

    /// <summary>
    /// Summarizes audit (SACL) rules, returning empty if they cannot be read.
    /// </summary>
    /// <remarks>Reading the SACL requires SeSecurityPrivilege and commonly throws for non-elevated callers,
    /// so failure is swallowed to keep the rest of the security summary usable.</remarks>
    private static string TrySummarizeAuditRules(FileSystemSecurity security)
    {
        try
        {
            return SummarizeAuditRules(security.GetAuditRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAuditRule>());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Resolves an identity (owner/group) to its string form, returning empty if it cannot be translated.</summary>
    /// <remarks>SID→account translation can fail (e.g. orphaned SIDs), so it is treated as best-effort.</remarks>
    private static string SafeIdentityToString(Func<IdentityReference?> factory)
    {
        try
        {
            return factory()?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Counts allow/deny/inherited DACL entries into a one-line summary (empty when there are none).</summary>
    private static string SummarizeAccessRules(IEnumerable<FileSystemAccessRule> rules)
    {
        var allow = 0;
        var deny = 0;
        var inherited = 0;

        foreach (var rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow)
            {
                allow++;
            }
            else
            {
                deny++;
            }

            if (rule.IsInherited)
            {
                inherited++;
            }
        }

        return allow == 0 && deny == 0
            ? string.Empty
            : $"Allow {allow}, Deny {deny}, Inherited {inherited}";
    }

    /// <summary>Counts success/failure SACL entries into a one-line summary (empty when there are none).</summary>
    private static string SummarizeAuditRules(IEnumerable<FileSystemAuditRule> rules)
    {
        var success = 0;
        var failure = 0;

        foreach (var rule in rules)
        {
            if ((rule.AuditFlags & AuditFlags.Success) != 0)
            {
                success++;
            }

            if ((rule.AuditFlags & AuditFlags.Failure) != 0)
            {
                failure++;
            }
        }

        return success == 0 && failure == 0
            ? string.Empty
            : $"Success {success}, Failure {failure}";
    }
}
