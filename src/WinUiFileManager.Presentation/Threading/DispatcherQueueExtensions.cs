namespace WinUiFileManager.Presentation.Threading;

/// <summary>
/// Awaitable wrappers around <see cref="Microsoft.UI.Dispatching.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueueHandler)"/>
/// that let callers marshal work onto the UI thread and <c>await</c> its completion/result, surfacing
/// exceptions back to the awaiter instead of swallowing them on the dispatcher.
/// </summary>
/// <remarks>
/// Used wherever library/background code must touch UI-affine state (AGENTS.md §6). The returned
/// <see cref="Task"/> faults with <see cref="InvalidOperationException"/> if the work cannot even be
/// enqueued (e.g. the dispatcher is shutting down), so callers never await a task that will never run.
/// </remarks>
public static class DispatcherQueueExtensions
{
    /// <summary>Runs <paramref name="action"/> on the dispatcher and completes the returned task when it
    /// finishes (or faults it with the action's exception, or with
    /// <see cref="InvalidOperationException"/> if it could not be enqueued).</summary>
    public static Task RunAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue, Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher queue."));
        }

        return tcs.Task;
    }

    /// <summary>Runs <paramref name="func"/> on the dispatcher and completes the returned task with its
    /// result (or faults it with the function's exception, or with
    /// <see cref="InvalidOperationException"/> if it could not be enqueued).</summary>
    public static Task<T> RunAsync<T>(this Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue,
        Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("Failed to enqueue work on the dispatcher queue."));
        }

        return tcs.Task;
    }
}
