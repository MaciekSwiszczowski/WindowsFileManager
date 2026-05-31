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
/// Lifetime: DI singleton; registers in <see cref="Initialize"/>, unregisters in <see cref="Dispose"/>,
/// which is effectively unreachable because the container is never disposed (AGENTS.md §5).
/// Threading: answered via <c>message.Reply(Task.Run(...))</c>; <see cref="Load"/> runs on the thread pool
/// under <see cref="LoadTimeout"/>. SACL/owner reads can throw on privilege/access issues, so they are
/// wrapped in best-effort try/catch helpers that degrade to empty strings rather than failing the whole load.
/// </remarks>
public sealed class InspectorSecurityDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<InspectorSecurityDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorSecurityDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorSecurityDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>Registers the request handler. Not idempotent — call exactly once (AGENTS.md §4).</summary>
    public void Initialize()
    {
        _messenger.Register<InspectorSecurityDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    /// <summary>Unregisters from the messenger (idempotent). See type remarks: effectively never called.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Reads and summarizes the security descriptor for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path and cancellation token.</param>
    /// <returns>Security summary, or <see cref="FileSecurityDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Real cancellation is rethrown; other errors are logged and degraded to empty.</remarks>
    private FileSecurityDiagnosticsDetails Load(InspectorSecurityDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var path = message.Path.DisplayPath;
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

            var owner = SafeIdentityToString(() => security.GetOwner(typeof(NTAccount)));
            var group = SafeIdentityToString(() => security.GetGroup(typeof(NTAccount)));
            var daclSummary = SummarizeAccessRules(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
            var saclSummary = TrySummarizeAuditRules(security);

            return new FileSecurityDiagnosticsDetails(
                owner,
                group,
                daclSummary,
                saclSummary,
                !security.AreAccessRulesProtected,
                security.AreAccessRulesProtected);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Rethrow only for genuine caller cancellation; timeout cancellation degrades to empty below.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector security diagnostics for {Path}", message.Path.DisplayPath);
            return FileSecurityDiagnosticsDetails.Empty;
        }
    }

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
