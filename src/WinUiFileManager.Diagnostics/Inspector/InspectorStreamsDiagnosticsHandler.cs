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
/// <remarks>
/// Lifetime: DI singleton; registers in <see cref="Initialize"/>, unregisters in <see cref="Dispose"/>,
/// which is effectively unreachable because the container is never disposed (AGENTS.md §5).
/// Threading: answered via <c>message.Reply(Task.Run(...))</c>; <see cref="Load"/> runs on the thread pool.
/// The timeout is a longer <see cref="LoadTimeout"/> (20s) than the other handlers because stream
/// enumeration on large or slow/cloud-backed files can take noticeably longer.
/// </remarks>
public sealed class InspectorStreamsDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(20);

    private readonly IAlternateDataStreamInterop _alternateDataStreamInterop;
    private readonly ILogger<InspectorStreamsDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorStreamsDiagnosticsHandler(
        IMessenger messenger,
        IAlternateDataStreamInterop alternateDataStreamInterop,
        ILogger<InspectorStreamsDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _alternateDataStreamInterop = alternateDataStreamInterop;
        _logger = logger;
    }

    /// <summary>Registers the request handler. Not idempotent — call exactly once (AGENTS.md §4).</summary>
    public void Initialize()
    {
        _messenger.Register<InspectorStreamsDiagnosticsRequestMessage>(this,
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
    /// Enumerates alternate data streams for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path and cancellation token.</param>
    /// <returns>Stream details, or <see cref="FileStreamDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Real cancellation is rethrown; other errors are logged and degraded to empty.</remarks>
    private FileStreamDiagnosticsDetails Load(InspectorStreamsDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var streams = _alternateDataStreamInterop.EnumerateAlternateDataStreamDisplayLines(message.Path.DisplayPath);
            return new FileStreamDiagnosticsDetails(streams.Count.ToString(), streams);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Rethrow only for genuine caller cancellation; timeout cancellation degrades to empty below.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector stream diagnostics for {Path}", message.Path.DisplayPath);
            return FileStreamDiagnosticsDetails.Empty;
        }
    }
}
