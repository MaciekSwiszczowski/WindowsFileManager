using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Base class for the deferred inspector field loaders. Implements the shared cancellation/superseding machinery
/// (each <see cref="Load"/> cancels the previous one and stamps a monotonically increasing version), the loading
/// flag lifecycle on the fields, and disposal. Subclasses only supply their field keys and the typed
/// fetch (<see cref="LoadDiagnosticsAsync"/>) / apply (<see cref="ApplyAsync"/>) steps.
/// </summary>
/// <typeparam name="TDiagnostics">The diagnostics record type this loader fetches and applies.</typeparam>
/// <remarks>
/// <para>
/// Race handling: a load is identified by <see cref="_loadVersion"/> (bumped via <see cref="Interlocked"/>) and a
/// dedicated <see cref="CancellationTokenSource"/>. Results are applied only if the load is still current
/// (<see cref="CanApply"/>): not disposed, not cancelled, and the version matches. This guards against a stale
/// async result overwriting fields after the selection has moved on.
/// </para>
/// <para>
/// The async load is intentionally fire-and-forget (<c>_ = LoadAsync(...)</c>): there is no caller to await it, and
/// the <c>finally</c> block clears the loading flag and disposes the CTS only when it is still the active one.
/// </para>
/// </remarks>
internal abstract class InspectorDeferredFieldLoaderBase<TDiagnostics> :
    IInspectorDeferredFieldLoader,
    IInspectorDeferredFieldLoaderInitializer
{
    private InspectorFieldValueUpdater? _fieldValueUpdater;
    private CancellationTokenSource? _loadCancellation;
    private long _loadVersion;
    private bool _disposed;

    /// <summary>The shared value updater; throws if accessed before <see cref="Initialize"/>.</summary>
    protected InspectorFieldValueUpdater FieldValueUpdater =>
        _fieldValueUpdater ?? throw new InvalidOperationException($"{GetType().Name} must be initialized before loading.");

    /// <summary>The field keys this loader owns; used to drive their shared loading state.</summary>
    protected abstract IReadOnlyList<string> FieldKeys { get; }

    /// <summary>Stores the value updater. Throws if called more than once (AGENTS.md idempotent-init expectation).</summary>
    /// <exception cref="InvalidOperationException">Thrown when already initialized.</exception>
    public void Initialize(InspectorFieldValueUpdater fieldValueUpdater)
    {
        if (_fieldValueUpdater is not null)
        {
            throw new InvalidOperationException($"{GetType().Name} is already initialized.");
        }

        _fieldValueUpdater = fieldValueUpdater;
    }

    /// <summary>
    /// Begins loading for the selection: cancels the previous load, marks the fields loading, and kicks off the
    /// async fetch/apply. Parent-entry rows (null model) just cancel and return.
    /// </summary>
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

    /// <summary>Cancels the in-flight load and clears the loading flag on the fields.</summary>
    public void Cancel() => CancelCurrentLoad(clearLoading: true);

    /// <summary>Cancels any in-flight load and marks the loader disposed. Idempotent via <see cref="_disposed"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelCurrentLoad(clearLoading: true);
    }

    /// <summary>Subclass hook: fetches the diagnostics for <paramref name="path"/> (typically via a request message).</summary>
    protected abstract Task<TDiagnostics> LoadDiagnosticsAsync(NormalizedPath path, CancellationToken cancellationToken);

    /// <summary>Subclass hook: writes the fetched diagnostics into the inspector fields. Runs UI-affine.</summary>
    protected abstract Task ApplyAsync(TDiagnostics diagnostics);

    /// <summary>
    /// Runs one fetch/apply cycle. Applies the result only if still current (<see cref="CanApply"/>); swallows
    /// cancellation for this load; and in <c>finally</c> clears the loading flag and disposes the CTS, but only
    /// when this load is still the active one (so a superseding load isn't clobbered).
    /// </summary>
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

    /// <summary>
    /// Invalidates the current load by bumping the version and cancelling its CTS; optionally clears the loading
    /// flag. Bumping the version first ensures a racing <see cref="LoadAsync"/> sees itself as stale.
    /// </summary>
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

    /// <summary>True when results for <paramref name="version"/> may still be applied: not disposed, not cancelled, and still the current version.</summary>
    private bool CanApply(long version, CancellationToken cancellationToken) =>
        !_disposed
        && !cancellationToken.IsCancellationRequested
        && Interlocked.Read(ref _loadVersion) == version;
}
