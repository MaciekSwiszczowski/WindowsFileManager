using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorStreamsDiagnosticsRequestMessage"/> by
/// enumerating a file's NTFS alternate data streams (count plus per-stream display lines).
/// </summary>
public sealed class InspectorStreamsDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        InspectorStreamsDiagnosticsRequestMessage,
        FileStreamDiagnosticsDetails,
        InspectorStreamsDiagnosticsResponseMessage>
{
    private readonly IAlternateDataStreamInterop _alternateDataStreamInterop;

    public InspectorStreamsDiagnosticsHandler(
        IMessenger messenger,
        IAlternateDataStreamInterop alternateDataStreamInterop,
        ILogger<InspectorStreamsDiagnosticsHandler> logger)
        : base(messenger, logger)
    {
        _alternateDataStreamInterop = alternateDataStreamInterop;
    }

    /// <summary>
    /// Enumerates alternate data streams for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>Stream details, or <see cref="FileStreamDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to empty.</remarks>
    protected override Task<FileStreamDiagnosticsDetails> LoadAsync(
        InspectorStreamsDiagnosticsRequestMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var streams = _alternateDataStreamInterop.EnumerateAlternateDataStreamDisplayLines(message.Path.DisplayPath);
        return Task.FromResult(new FileStreamDiagnosticsDetails(streams.Count.ToString(), streams));
    }

    protected override InspectorStreamsDiagnosticsResponseMessage CreateResponse(FileStreamDiagnosticsDetails diagnostics) =>
        new(diagnostics);

    protected override FileStreamDiagnosticsDetails GetEmptyDiagnostics(InspectorStreamsDiagnosticsRequestMessage request) =>
        FileStreamDiagnosticsDetails.Empty;
}
