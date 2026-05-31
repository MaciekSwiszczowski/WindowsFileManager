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
        Cancel();

        if (selectedItem.Model is not { } model)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        var version = Interlocked.Increment(ref _loadVersion);
        _loadCancellation = cancellation;

        _ = LoadAsync(model.FullPath, version, cancellation);
    }

    public void Cancel()
    {
        Interlocked.Increment(ref _loadVersion);

        var cancellation = _loadCancellation;
        _loadCancellation = null;
        cancellation?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    protected abstract Task<TDiagnostics> LoadDiagnosticsAsync(NormalizedPath path, CancellationToken cancellationToken);

    protected abstract void Apply(TDiagnostics diagnostics);

    private async Task LoadAsync(NormalizedPath path, long version, CancellationTokenSource cancellation)
    {
        try
        {
            var diagnostics = await LoadDiagnosticsAsync(path, cancellation.Token);
            if (CanApply(version, cancellation.Token))
            {
                Apply(diagnostics);
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
            }

            cancellation.Dispose();
        }
    }

    private bool CanApply(long version, CancellationToken cancellationToken) =>
        !_disposed
        && !cancellationToken.IsCancellationRequested
        && Interlocked.Read(ref _loadVersion) == version;
}
