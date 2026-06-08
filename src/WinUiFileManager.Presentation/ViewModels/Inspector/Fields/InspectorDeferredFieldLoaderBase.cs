using AsyncAwaitBestPractices;
using R3;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Base class for deferred inspector field loaders using normal request/response messages. It owns the
/// response subscription, loading-state lifecycle, and disposal. Concrete loaders only
/// provide field keys and diagnostics application.
/// </summary>
internal abstract class InspectorDeferredFieldLoaderBase<TResponse, TDiagnostics> :
    IInspectorDeferredFieldLoader,
    IInspectorDeferredFieldLoaderInitializer
    where TResponse : class, IInspectorDiagnosticsResponseMessage<TDiagnostics>
{
    private readonly ILogger _logger;
    private readonly IFileManagerMessenger _messenger;
    private readonly SynchronizationContext _uiSynchronizationContext;
    private InspectorFieldValueUpdater? _fieldValueUpdater;
    private IDisposable? _responseSubscription;
    private bool _hasPendingRequest;
    private bool _disposed;

    protected InspectorDeferredFieldLoaderBase(IFileManagerMessenger messenger, SynchronizationContext uiSynchronizationContext, ILogger logger)
    {
        _messenger = messenger;
        _uiSynchronizationContext = uiSynchronizationContext;
        _logger = logger;
    }

    /// <summary>The shared value updater; throws if accessed before <see cref="Initialize"/>.</summary>
    protected InspectorFieldValueUpdater FieldValueUpdater =>
        _fieldValueUpdater ?? throw new InvalidOperationException($"{GetType().Name} must be initialized before loading.");

    /// <summary>The field keys this loader owns; used to drive their shared loading state.</summary>
    protected abstract IReadOnlyList<string> FieldKeys { get; }

    /// <summary>Writes the response payload into inspector fields. Runs UI-affine.</summary>
    protected abstract Task ApplyAsync(TDiagnostics diagnostics);

    public void Initialize(InspectorFieldValueUpdater fieldValueUpdater)
    {
        if (_fieldValueUpdater is not null)
        {
            throw new InvalidOperationException($"{GetType().Name} is already initialized.");
        }

        _fieldValueUpdater = fieldValueUpdater;
        _responseSubscription?.Dispose();
        _responseSubscription = _messenger
            .CreateObservable<TResponse>()
            .ObserveOn(_uiSynchronizationContext)
            .Subscribe(response => ApplyResponseAsync(response).SafeFireAndForget(OnApplyException));
    }

    public void Prepare(FileListingRow selectedItem)
    {
        _ = FieldValueUpdater;
        CancelCurrentLoad(clearLoading: false);

        if (selectedItem.Model is null)
        {
            return;
        }

        FieldValueUpdater.SetLoading(FieldKeys, isLoading: true);
    }

    public void Load(FileListingRow selectedItem)
    {
        _ = FieldValueUpdater;
        CancelCurrentLoad(clearLoading: false);

        if (selectedItem.Model is null)
        {
            return;
        }

        _hasPendingRequest = true;
        FieldValueUpdater.SetLoading(FieldKeys, isLoading: true);
    }

    public void Cancel() => CancelCurrentLoad(clearLoading: true);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the response subscription and clears loading state. Idempotent.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            _responseSubscription?.Dispose();
            _responseSubscription = null;
            CancelCurrentLoad(clearLoading: true);
        }
    }

    private async Task ApplyResponseAsync(TResponse response)
    {
        if (_disposed || !_hasPendingRequest)
        {
            DisposeDiagnostics(response.Diagnostics);
            return;
        }

        _hasPendingRequest = false;
        try
        {
            await ApplyAsync(response.Diagnostics).ConfigureAwait(true);
        }
        finally
        {
            DisposeDiagnostics(response.Diagnostics);
            FieldValueUpdater.SetLoading(FieldKeys, isLoading: false);
        }
    }

    private void OnApplyException(Exception exception)
        => _logger.LogWarning(exception, "Failed to apply {Loader} inspector diagnostics.", GetType().Name);

    private static void DisposeDiagnostics(TDiagnostics diagnostics)
    {
        // IDISP007: the diagnostics payload is owned by this loader (single-owner pooled buffer), not injected — we
        // are the consumer responsible for releasing it once applied or discarded.
#pragma warning disable IDISP007
        if (diagnostics is IDisposable disposable)
        {
            disposable.Dispose();
        }
#pragma warning restore IDISP007
    }

    private void CancelCurrentLoad(bool clearLoading)
    {
        _hasPendingRequest = false;

        if (clearLoading && _fieldValueUpdater is not null)
        {
            _fieldValueUpdater.SetLoading(FieldKeys, isLoading: false);
        }
    }
}
