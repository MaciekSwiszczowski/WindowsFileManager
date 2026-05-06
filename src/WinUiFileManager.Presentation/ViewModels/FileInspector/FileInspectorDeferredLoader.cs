using System.Reactive.Concurrency;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class FileInspectorDeferredLoader : IDisposable
{
    private readonly ISchedulerProvider _schedulers;
    private readonly ILogger<FileInspectorViewModel> _logger;
    private readonly Func<FileInspectorSelection, CancellationToken, IAsyncEnumerable<FileInspectorDeferredBatchResult>> _loadBatches;
    private readonly Action<FileInspectorDeferredBatchResult> _applyBatch;
    private readonly Func<bool> _isDisposed;
    private CancellationTokenSource _cancellation = new();
    private bool _disposed;

    public FileInspectorDeferredLoader(
        ISchedulerProvider schedulers,
        ILogger<FileInspectorViewModel> logger,
        Func<FileInspectorSelection, CancellationToken, IAsyncEnumerable<FileInspectorDeferredBatchResult>> loadBatches,
        Action<FileInspectorDeferredBatchResult> applyBatch,
        Func<bool> isDisposed)
    {
        _schedulers = schedulers;
        _logger = logger;
        _loadBatches = loadBatches;
        _applyBatch = applyBatch;
        _isDisposed = isDisposed;
    }

    public void Start(FileInspectorSelection selection)
    {
        Cancel();

        if (!selection.CanLoadDeferred)
        {
            return;
        }

        _ = LoadAsync(selection, _cancellation.Token);
    }

    public void Cancel()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async Task LoadAsync(FileInspectorSelection selection, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(async () =>
            {
                await foreach (var batch in _loadBatches(selection, cancellationToken)
                    .ConfigureAwait(false))
                {
                    await RunOnMainThreadAsync(
                        () => _applyBatch(batch),
                        cancellationToken).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _isDisposed())
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspector deferred batch streaming failed");
        }
    }

    private Task RunOnMainThreadAsync(Action action, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _schedulers.MainThread.Schedule(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }
}
