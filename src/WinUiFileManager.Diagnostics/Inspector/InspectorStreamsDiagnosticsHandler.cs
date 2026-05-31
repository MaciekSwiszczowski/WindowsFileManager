using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

public sealed class InspectorStreamsDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly IFileIdentityService _fileIdentityService;
    private readonly ILogger<InspectorStreamsDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorStreamsDiagnosticsHandler(IMessenger messenger, IFileIdentityService fileIdentityService, ILogger<InspectorStreamsDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _fileIdentityService = fileIdentityService;
        _logger = logger;
    }

    public void Initialize()
    {
        _messenger.Register<InspectorStreamsDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => LoadAsync(message))));
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

    private async Task<FileStreamDiagnosticsDetails> LoadAsync(InspectorStreamsDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            return await _fileIdentityService.GetStreamDiagnosticsAsync(
                message.Path.DisplayPath,
                timeoutCts.Token).ConfigureAwait(false);
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
