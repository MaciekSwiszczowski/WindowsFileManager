using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Async request asking the Diagnostics layer which processes/services hold a file open (via Restart
/// Manager). Sent by the inspector view model when a file is selected; the locks handler replies with a
/// <see cref="FileLockDiagnostics"/>.
/// </summary>
public sealed class InspectorLocksDiagnosticsRequestMessage : AsyncRequestMessage<FileLockDiagnostics>, IFileManagerMessengerMessage
{
    /// <param name="path">The file to inspect.</param>
    /// <param name="cancellationToken">Cancels the diagnostics computation.</param>
    public InspectorLocksDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    /// <summary>The file being inspected.</summary>
    public NormalizedPath Path { get; }

    /// <summary>Cancels the diagnostics work; the handler should flow this through its I/O.</summary>
    public CancellationToken CancellationToken { get; }
}
