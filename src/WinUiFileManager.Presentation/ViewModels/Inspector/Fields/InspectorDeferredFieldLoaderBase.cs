using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal abstract class InspectorDeferredFieldLoaderBase<TDiagnostics> :
    IInspectorDeferredFieldLoader,
    IInspectorDeferredFieldLoaderInitializer
{
    private InspectorFieldValueUpdater? _fieldValueUpdater;
    private CancellationTokenSource? _loadCancellation;
    private long _loadVersion;
    private bool _disposed;

    protected InspectorFieldValueUpdater FieldValueUpdater =>
        _fieldValueUpdater ?? throw new InvalidOperationException($"{GetType().Name} must be initialized before loading.");

    protected abstract IReadOnlyList<string> FieldKeys { get; }

    public void Initialize(InspectorFieldValueUpdater fieldValueUpdater)
    {
        if (_fieldValueUpdater is not null)
        {
            throw new InvalidOperationException($"{GetType().Name} is already initialized.");
        }

        _fieldValueUpdater = fieldValueUpdater;
    }

    public void Load(SpecFileEntryViewModel selectedItem)
    {
        _ = FieldValueUpdater;
        CancelCurrentLoad(clearLoading: false);

        if (selectedItem.Model is not { } model)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        var version = Interlocked.Increment(ref _loadVersion);
        _loadCancellation = cancellation;
        FieldValueUpdater.SetLoading(FieldKeys, isLoading: true);

        _ = LoadAsync(model.FullPath, version, cancellation);
    }

    public void Cancel() => CancelCurrentLoad(clearLoading: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelCurrentLoad(clearLoading: true);
    }

    protected abstract Task<TDiagnostics> LoadDiagnosticsAsync(NormalizedPath path, CancellationToken cancellationToken);

    protected abstract Task ApplyAsync(TDiagnostics diagnostics);

    private async Task LoadAsync(NormalizedPath path, long version, CancellationTokenSource cancellation)
    {
        try
        {
            var diagnostics = await LoadDiagnosticsAsync(path, cancellation.Token);
            if (CanApply(version, cancellation.Token))
            {
                await ApplyAsync(diagnostics);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                FieldValueUpdater.SetLoading(FieldKeys, isLoading: false);
            }

            cancellation.Dispose();
        }
    }

    private void CancelCurrentLoad(bool clearLoading)
    {
        Interlocked.Increment(ref _loadVersion);

        var cancellation = _loadCancellation;
        _loadCancellation = null;
        cancellation?.Cancel();

        if (clearLoading && _fieldValueUpdater is not null)
        {
            _fieldValueUpdater.SetLoading(FieldKeys, isLoading: false);
        }
    }

    private bool CanApply(long version, CancellationToken cancellationToken) =>
        !_disposed
        && !cancellationToken.IsCancellationRequested
        && Interlocked.Read(ref _loadVersion) == version;
}
