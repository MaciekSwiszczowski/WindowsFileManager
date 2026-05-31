using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

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

    public void Initialize()
    {
        _messenger.Register<InspectorStreamsDiagnosticsRequestMessage>(this,
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
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector stream diagnostics for {Path}", message.Path.DisplayPath);
            return FileStreamDiagnosticsDetails.Empty;
        }
    }
}
