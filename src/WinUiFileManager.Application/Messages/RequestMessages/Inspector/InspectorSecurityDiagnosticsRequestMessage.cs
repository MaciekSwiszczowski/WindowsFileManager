using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Async request asking the Diagnostics layer for a file's security-descriptor summary (owner, ACLs).
/// Sent by the inspector view model when a file is selected; the security handler replies with a
/// <see cref="FileSecurityDiagnosticsDetails"/>.
/// </summary>
public sealed class InspectorSecurityDiagnosticsRequestMessage : AsyncRequestMessage<FileSecurityDiagnosticsDetails>, IFileManagerMessengerMessage
{
    /// <param name="path">The file to inspect.</param>
    /// <param name="cancellationToken">Cancels the diagnostics computation.</param>
    public InspectorSecurityDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    /// <summary>The file being inspected.</summary>
    public NormalizedPath Path { get; }

    /// <summary>Cancels the diagnostics work; the handler should flow this through its I/O.</summary>
    public CancellationToken CancellationToken { get; }
}
