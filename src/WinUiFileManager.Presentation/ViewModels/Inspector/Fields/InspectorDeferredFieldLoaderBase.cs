using System.Reactive.Linq;
using AsyncAwaitBestPractices;
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
    private readonly IMessenger _messenger;
    private readonly ISchedulerProvider _schedulers;
    private InspectorFieldValueUpdater? _fieldValueUpdater;
    private IDisposable? _responseSubscription;
    private bool _hasPendingRequest;
    private bool _disposed;

    protected InspectorDeferredFieldLoaderBase(IMessenger messenger, ISchedulerProvider schedulers, ILogger logger)
    {
        _messenger = messenger;
        _schedulers = schedulers;
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
        _responseSubscription = _messenger
            .CreateObservable<TResponse>()
            .ObserveOn(_schedulers.MainThread)
            .Subscribe(response => ApplyResponseAsync(response).SafeFireAndForget(OnApplyException));
    }

    public void Prepare(SpecFileEntryViewModel selectedItem)
    {
        _ = FieldValueUpdater;
        CancelCurrentLoad(clearLoading: false);

        if (selectedItem.Model is null)
        {
            return;
        }

        FieldValueUpdater.SetLoading(FieldKeys, isLoading: true);
    }

    public void Load(SpecFileEntryViewModel selectedItem)
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _responseSubscription?.Dispose();
        _responseSubscription = null;
        CancelCurrentLoad(clearLoading: true);
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
            await ApplyAsync(response.Diagnostics);
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
        if (diagnostics is IDisposable disposable)
        {
            disposable.Dispose();
        }
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
