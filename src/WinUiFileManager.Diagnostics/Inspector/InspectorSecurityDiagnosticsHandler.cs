using System.Security.AccessControl;
using System.Security.Principal;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

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

    public void Initialize()
    {
        _messenger.Register<InspectorSecurityDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

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
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector security diagnostics for {Path}", message.Path.DisplayPath);
            return FileSecurityDiagnosticsDetails.Empty;
        }
    }

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
