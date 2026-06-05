using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.Threading;

/// <summary>
/// WinUI <see cref="UiDispatcherQueue"/> implementation of <see cref="IUiThreadDispatcher"/>.
/// Presentation-owned infrastructure for UI-affine work during the R3 migration.
/// </summary>
/// <remarks>
/// The dispatcher queue is captured by the composition root on the UI thread and reused for the application
/// lifetime. This type only owns marshalling; it does not own reactive timing or collection projection.
/// </remarks>
internal sealed class DispatcherQueueUiThreadDispatcher : IUiThreadDispatcher
{
    private readonly UiDispatcherQueue _dispatcherQueue;

    public DispatcherQueueUiThreadDispatcher(UiDispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    /// <inheritdoc />
    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // action.Invoke is a method-group conversion to DispatcherQueueHandler, avoiding the display-class
        // closure a `() => action()` lambda would allocate to capture `action`.
        if (!_dispatcherQueue.TryEnqueue(action.Invoke))
        {
            throw new InvalidOperationException("Failed to enqueue work on the UI dispatcher queue.");
        }
    }

    /// <inheritdoc />
    public Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        // Re-check once the work runs, in case cancellation is requested between enqueue and execution.
        // A post-enqueue cancellation surfaces as a faulted task wrapping OperationCanceledException.
        return _dispatcherQueue.RunAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
        });
    }
}
