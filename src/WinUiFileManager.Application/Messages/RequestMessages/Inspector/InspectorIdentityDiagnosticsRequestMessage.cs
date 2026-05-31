using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Async request asking the Diagnostics layer for a file's NTFS metadata and identity facts. Sent by the
/// inspector view model when a file is selected; <c>InspectorIdentityDiagnosticsHandler</c> replies with an
/// <see cref="InspectorIdentityDiagnosticsDetails"/>.
/// </summary>
public sealed class InspectorIdentityDiagnosticsRequestMessage : AsyncRequestMessage<InspectorIdentityDiagnosticsDetails>, IFileManagerMessengerMessage
{
    /// <param name="path">The file to inspect.</param>
    /// <param name="cancellationToken">Cancels the diagnostics computation.</param>
    public InspectorIdentityDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    /// <summary>The file being inspected.</summary>
    public NormalizedPath Path { get; }

    /// <summary>Cancels the diagnostics work; the handler should flow this through its I/O.</summary>
    public CancellationToken CancellationToken { get; }
}
