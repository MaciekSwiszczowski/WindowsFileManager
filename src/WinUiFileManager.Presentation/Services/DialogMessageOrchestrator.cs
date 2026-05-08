using System.Reactive.Linq;
using WinUiFileManager.Presentation.FileEntryTable.Data;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.Services;

internal sealed class DialogMessageOrchestrator : IDisposable
{
    private readonly IDisposable _subscription;

    public DialogMessageOrchestrator(
        UiDispatcherQueue dispatcherQueue,
        Func<ShowDialogMessage, Task<DialogResult>> showDialogAsync,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        ArgumentNullException.ThrowIfNull(showDialogAsync);
        ArgumentNullException.ThrowIfNull(logger);

        var scheduler = new DispatcherQueueScheduler(dispatcherQueue);
        _subscription = WeakReferenceMessenger.Default
            .CreateObservable<ShowDialogMessage>()
            .Select(QueuedDialogMessage.Create)
            .ObserveOn(scheduler)
            .Select(request => Observable.FromAsync(() => ProcessAsync(request, showDialogAsync)))
            .Concat()
            .Subscribe(
                static _ => { },
                ex => logger.LogError(ex, "Dialog message queue failed."));
    }

    public void Dispose() => _subscription.Dispose();

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

    private sealed class QueuedDialogMessage
    {
        private readonly TaskCompletionSource<DialogResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private QueuedDialogMessage(ShowDialogMessage message)
        {
            Message = message;
            message.Reply(_completion.Task);
        }

        public ShowDialogMessage Message { get; }

        public static QueuedDialogMessage Create(ShowDialogMessage message) => new(message);

        public void Complete(DialogResult result) => _completion.TrySetResult(result);

        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }
}
