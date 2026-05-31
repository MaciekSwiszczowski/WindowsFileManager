using CommunityToolkit.Mvvm.Messaging.Messages;
using WinUiFileManager.Application.Messaging;

namespace WinUiFileManager.Application.Messages.RequestMessages.Inspector;

/// <summary>
/// Async request asking the Diagnostics layer to compute a file's cloud/placeholder state. Sent by the
/// inspector view model when a file is selected; <c>InspectorCloudDiagnosticsHandler</c> replies with a
/// <see cref="FileCloudDiagnosticsDetails"/> (work runs on a background thread).
/// </summary>
public sealed class InspectorCloudDiagnosticsRequestMessage : AsyncRequestMessage<FileCloudDiagnosticsDetails>, IFileManagerMessengerMessage
{
    /// <param name="path">The file to inspect.</param>
    /// <param name="cancellationToken">Cancels the diagnostics computation (e.g. selection changed).</param>
    public InspectorCloudDiagnosticsRequestMessage(NormalizedPath path, CancellationToken cancellationToken)
    {
        Path = path;
        CancellationToken = cancellationToken;
    }

    /// <summary>The file being inspected.</summary>
    public NormalizedPath Path { get; }

    /// <summary>Cancels the diagnostics work; the handler should flow this through its I/O.</summary>
    public CancellationToken CancellationToken { get; }
}
