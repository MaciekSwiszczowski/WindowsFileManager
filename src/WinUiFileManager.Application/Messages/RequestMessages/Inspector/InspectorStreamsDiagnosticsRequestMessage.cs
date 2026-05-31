using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Async request asking the Diagnostics layer to enumerate a file's NTFS alternate data streams. Sent by
/// the inspector view model when a file is selected; the streams handler replies with a
/// <see cref="FileStreamDiagnosticsDetails"/>.
/// </summary>
public sealed class InspectorStreamsDiagnosticsRequestMessage : AsyncRequestMessage<FileStreamDiagnosticsDetails>, IFileManagerMessengerMessage
{
    /// <param name="path">The file to inspect.</param>
    /// <param name="cancellationToken">Cancels the diagnostics computation.</param>
    public InspectorStreamsDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    /// <summary>The file being inspected.</summary>
    public NormalizedPath Path { get; }

    /// <summary>Cancels the diagnostics work; the handler should flow this through its I/O.</summary>
    public CancellationToken CancellationToken { get; }
}
