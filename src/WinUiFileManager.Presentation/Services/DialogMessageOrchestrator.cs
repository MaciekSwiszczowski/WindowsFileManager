using R3;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueSynchronizationContext = Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext;

namespace WinUiFileManager.Presentation.Services;

/// <summary>
/// Serialises incoming <see cref="ShowDialogMessage"/>s onto the UI thread and shows them one at a time,
/// so two requests can never race to open overlapping <see cref="ContentDialog"/>s (WinUI throws if a
/// second dialog opens while one is showing). Owned and (re)created by <see cref="DialogService"/>.
/// </summary>
/// <remarks>
/// Built as a cold R3 pipeline: dialog messages are observed on the UI thread (a
/// <see cref="DispatcherQueueSynchronizationContext"/>, AGENTS.md §6) and processed with
/// <see cref="AwaitOperation.Sequential"/> so each dialog's task completes before the next begins (strict
/// FIFO, no concurrency). Each request replies its own <see cref="TaskCompletionSource{T}"/> task back to the
/// sender immediately, and the pipeline completes it with the dialog result. The single subscription is owned
/// here and disposed in <see cref="Dispose"/> (AGENTS.md §5), which unsubscribes from the messenger observable.
/// </remarks>
internal sealed class DialogMessageOrchestrator : IDisposable
{
    private readonly IDisposable _subscription;

    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public DialogMessageOrchestrator(
        UiDispatcherQueue dispatcherQueue,
        Func<ShowDialogMessage, Task<DialogResult>> showDialogAsync,
        ILogger logger,
        IFileManagerMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        ArgumentNullException.ThrowIfNull(showDialogAsync);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(messenger);

        var uiSynchronizationContext = new DispatcherQueueSynchronizationContext(dispatcherQueue);
        _subscription = messenger
            .CreateObservable<ShowDialogMessage>()
            .Select(QueuedDialogMessage.Create)
            .ObserveOn(uiSynchronizationContext)
            // AwaitOperation.Sequential guarantees strictly one dialog at a time (FIFO, no concurrency).
            .SubscribeAwait(
                async (request, _) => await ProcessAsync(request, showDialogAsync),
                ex => logger.LogError(ex, "Dialog message queue failed."),
                _ => { },
                AwaitOperation.Sequential);
    }

    /// <summary>Disposes the messenger subscription, stopping further dialog processing.</summary>
    public void Dispose() => _subscription.Dispose();

    /// <summary>Shows one queued dialog and completes its reply task with the result, mapping
    /// cancellation to <see cref="DialogResult.Dismissed"/> and surfacing other failures via the task.</summary>
    private static async Task ProcessAsync(
        QueuedDialogMessage request,
        Func<ShowDialogMessage, Task<DialogResult>> showDialogAsync)
    {
        try
        {
            request.Complete(await showDialogAsync(request.Message));
        }
        catch (OperationCanceledException)
        {
            request.Complete(DialogResult.Dismissed);
        }
        catch (Exception ex)
        {
            request.Fail(ex);
        }
    }

    /// <summary>
    /// Wraps a <see cref="ShowDialogMessage"/> with the <see cref="TaskCompletionSource{T}"/> that backs
    /// the reply task handed to the sender. The reply is wired up at construction (when the message is
    /// first observed) so the sender can await the result before the dialog has even been shown.
    /// </summary>
    private sealed class QueuedDialogMessage
    {
        // RunContinuationsAsynchronously avoids resuming the awaiter inline on the UI thread under lock.
        private readonly TaskCompletionSource<DialogResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private QueuedDialogMessage(ShowDialogMessage message)
        {
            Message = message;
            // Hand the awaitable result back to the requester immediately; completed later by the queue.
            message.Reply(_completion.Task);
        }

        public ShowDialogMessage Message { get; }

        public static QueuedDialogMessage Create(ShowDialogMessage message) => new(message);

        /// <summary>Completes the reply task with the dialog outcome.</summary>
        public void Complete(DialogResult result) => _completion.TrySetResult(result);

        /// <summary>Faults the reply task with an unexpected error.</summary>
        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }
}
