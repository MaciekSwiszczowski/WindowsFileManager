using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Base class for inspector diagnostics handlers. It owns messenger-to-Rx registration, latest-request-wins
/// processing via <c>Switch()</c>, background execution, response publishing, and disposal.
/// </summary>
public abstract class InspectorDiagnosticsHandlerBase<TRequest, TDiagnostics, TResponse> : IDisposable
    where TRequest : class, IInspectorDiagnosticsRequestMessage
    where TResponse : class, IInspectorDiagnosticsResponseMessage<TDiagnostics>
{
    private readonly ILogger _logger;
    private readonly IMessenger _messenger;
    private IDisposable? _requestSubscription;
    private bool _disposed;

    protected InspectorDiagnosticsHandlerBase(IMessenger messenger, ILogger logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>Registers the request pipeline. Not idempotent; call exactly once from startup.</summary>
    public void Initialize()
    {
        if (_requestSubscription is not null)
        {
            throw new InvalidOperationException($"{GetType().Name} is already initialized.");
        }

        _requestSubscription = _messenger
            .CreateObservable<TRequest>()
            .Select(request => Observable.FromAsync(cancellationToken => LoadWithFallbackOnBackgroundAsync(request, cancellationToken)))
            .Switch()
            .Subscribe(
                diagnostics => _messenger.Send(CreateResponse(diagnostics)),
                ex => _logger.LogError(ex, "{Handler} diagnostics pipeline failed.", GetType().Name));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _requestSubscription?.Dispose();
        _requestSubscription = null;
    }

    /// <summary>Loads diagnostics for one request. Runs on a thread-pool thread.</summary>
    protected abstract Task<TDiagnostics> LoadAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>Creates the response message published for the latest completed request.</summary>
    protected abstract TResponse CreateResponse(TDiagnostics diagnostics);

    /// <summary>Fallback diagnostics used when loading fails.</summary>
    protected abstract TDiagnostics GetEmptyDiagnostics(TRequest request);

    private Task<TDiagnostics> LoadWithFallbackOnBackgroundAsync(TRequest request, CancellationToken cancellationToken)
    {
        return Task.Run(() => LoadWithFallbackAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<TDiagnostics> LoadWithFallbackAsync(TRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await LoadAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load {Handler} diagnostics for {Path}", GetType().Name, request.Path.DisplayPath);
            return GetEmptyDiagnostics(request);
        }
    }
}
