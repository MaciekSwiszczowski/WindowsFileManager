using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Base class for inspector diagnostics handlers. It owns messenger registration, latest-request-wins response
/// suppression, background execution, response publishing, and disposal.
/// </summary>
public abstract class InspectorDiagnosticsHandlerBase<TDiagnostics, TResponse> : IDisposable
    where TResponse : class, IInspectorDiagnosticsResponseMessage<TDiagnostics>
{
    private readonly ILogger _logger;
    private readonly IMessenger _messenger;
    private int _disposed;
    private int _initialized;
    private long _requestVersion;

    protected InspectorDiagnosticsHandlerBase(IMessenger messenger, ILogger logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>Registers the request pipeline. Not idempotent; call exactly once from startup.</summary>
    public void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            throw new InvalidOperationException($"{GetType().Name} is already initialized.");
        }

        _messenger.Register<InspectorDiagnosticsRequestMessage>(this, OnRequest);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _messenger.UnregisterAll(this);
    }

    /// <summary>Loads diagnostics for one request. Runs on a thread-pool thread.</summary>
    protected abstract Task<TDiagnostics> LoadAsync(InspectorDiagnosticsRequestMessage request);

    /// <summary>Creates the response message published for the latest completed request.</summary>
    protected abstract TResponse CreateResponse(TDiagnostics diagnostics);

    /// <summary>Fallback diagnostics used when loading fails.</summary>
    protected abstract TDiagnostics GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request);

    private void OnRequest(object recipient, InspectorDiagnosticsRequestMessage message)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var requestVersion = Interlocked.Increment(ref _requestVersion);
        Task.Run(() => ProcessRequestAsync(message, requestVersion))
            .SafeFireAndForget(OnProcessRequestException);
    }

    private async Task ProcessRequestAsync(InspectorDiagnosticsRequestMessage request, long requestVersion)
    {
        var diagnostics = await LoadWithFallbackAsync(request).ConfigureAwait(false);
        // Native/WinRT diagnostics loads are not reliably cancellable; stale completions are suppressed here.
        if (Volatile.Read(ref _disposed) == 0 && requestVersion == Volatile.Read(ref _requestVersion))
        {
            _messenger.Send(CreateResponse(diagnostics));
        }
    }

    private void OnProcessRequestException(Exception exception)
        => _logger.LogError(exception, "{Handler} diagnostics pipeline failed.", GetType().Name);

    private async Task<TDiagnostics> LoadWithFallbackAsync(InspectorDiagnosticsRequestMessage request)
    {
        try
        {
            return await LoadAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load {Handler} diagnostics for {Path}", GetType().Name, request.Path.DisplayPath);
            return GetEmptyDiagnostics(request);
        }
    }
}
