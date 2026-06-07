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
    private readonly Func<TDiagnostics, TResponse> _responseFactory;
    private int _disposed;
    private int _initialized;
    private long _requestVersion;

    protected InspectorDiagnosticsHandlerBase(
        IMessenger messenger,
        ILogger logger,
        Func<TDiagnostics, TResponse> responseFactory)
    {
        _messenger = messenger;
        _logger = logger;
        _responseFactory = responseFactory;
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the messenger registration so the strong-reference messenger stops rooting this handler. Idempotent.
    /// Derived classes that own disposable resources may override and call <c>base.Dispose(disposing)</c>.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _messenger.UnregisterAll(this);
        }
    }

    /// <summary>Loads diagnostics for one request. Runs on a thread-pool thread.</summary>
    protected abstract Task<TDiagnostics> LoadAsync(InspectorDiagnosticsRequestMessage message);

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
            .SafeFireAndForget(exception => _logger.LogError(exception, "{Handler} diagnostics pipeline failed.", GetType().Name));
    }

    private async Task ProcessRequestAsync(InspectorDiagnosticsRequestMessage request, long requestVersion)
    {
        var diagnostics = await LoadWithFallbackAsync(request).ConfigureAwait(false);
        // Native/WinRT diagnostics loads are not reliably cancellable; stale completions are suppressed here.
        if (Volatile.Read(ref _disposed) == 0 && requestVersion == Volatile.Read(ref _requestVersion))
        {
            _messenger.Send(_responseFactory(diagnostics));
        }
        else
        {
            // A newer request superseded this one (or the handler was disposed): the response is never published,
            // so release any pooled/native resources the abandoned diagnostics owns.
            (diagnostics as IDisposable)?.Dispose();
        }
    }

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
