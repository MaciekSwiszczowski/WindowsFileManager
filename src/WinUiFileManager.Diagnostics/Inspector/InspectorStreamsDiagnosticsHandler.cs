using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> by
/// enumerating a file's NTFS alternate data streams (count plus per-stream display lines).
/// </summary>
public sealed class InspectorStreamsDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
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
    protected override Task<FileStreamDiagnosticsDetails> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        var streams = _alternateDataStreamInterop.EnumerateAlternateDataStreamDisplayLines(message.Path.DisplayPath);

        // Common case (no alternate streams): reuse the shared sentinel so we avoid a new details record.
        var details = streams.Count == 0
            ? FileStreamDiagnosticsDetails.Empty
            : new FileStreamDiagnosticsDetails(streams.Count, streams);
        return Task.FromResult(details);
    }

    protected override InspectorStreamsDiagnosticsResponseMessage CreateResponse(FileStreamDiagnosticsDetails diagnostics) =>
        new(diagnostics);

    protected override FileStreamDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileStreamDiagnosticsDetails.Empty;
}
