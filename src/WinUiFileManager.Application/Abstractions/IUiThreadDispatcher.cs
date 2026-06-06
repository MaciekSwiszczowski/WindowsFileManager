namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Marshals work to the application's UI thread without exposing WinUI, Rx schedulers, or R3 providers to
/// application-layer consumers. Presentation supplies the concrete dispatcher.
/// </summary>
/// <remarks>
/// This is the migration seam away from scheduler abstractions for code that only needs
/// UI-affine execution, not reactive scheduling. Implementations must preserve exception flow for
/// <see cref="RunAsync(Action, CancellationToken)"/> so background callers can observe dispatcher failures.
/// </remarks>
public interface IUiThreadDispatcher
{
    /// <summary>True when the caller is already executing on the UI thread owned by this dispatcher.</summary>
    public bool HasThreadAccess { get; }

    /// <summary>
    /// Enqueues fire-and-forget UI work. Throws if the action cannot be enqueued, for example during shutdown.
    /// </summary>
    /// <param name="action">The UI-affine work to run.</param>
    public void Post(Action action);

    /// <summary>
    /// Enqueues UI work and completes when it has run, propagating any exception thrown by <paramref name="action"/>.
    /// </summary>
    /// <param name="action">The UI-affine work to run.</param>
    /// <param name="cancellationToken">Cancellation checked before enqueuing and again when the queued work runs.</param>
    public Task RunAsync(Action action, CancellationToken cancellationToken = default);
}
