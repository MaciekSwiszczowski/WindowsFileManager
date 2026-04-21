using WinUiFileManager.Domain.Operations;
using WinUiFileManager.Domain.Results;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class BlockingFileOperationService : IFileOperationService
{
    private readonly TaskCompletionSource _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public int ExecuteCallCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public OperationPlan? LastPlan { get; private set; }

    public async Task<OperationSummary> ExecuteAsync(
        OperationPlan plan,
        IProgress<OperationProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        ExecuteCallCount++;
        LastPlan = plan;
        LastCancellationToken = cancellationToken;
        _started.TrySetResult();

        var firstItem = plan.Items.FirstOrDefault();
        progress?.Report(new OperationProgressEvent(
            plan.Type,
            plan.Items.Count,
            0,
            plan.Items.Sum(static item => Math.Max(item.EstimatedSize, 0)),
            0,
            firstItem?.SourcePath,
            "Processing item"));

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        var wasCancelled = cancellationToken.IsCancellationRequested;
        return new OperationSummary(
            plan.Type,
            wasCancelled ? OperationStatus.Cancelled : OperationStatus.Succeeded,
            plan.Items.Count,
            wasCancelled ? 0 : plan.Items.Count,
            0,
            0,
            0,
            wasCancelled,
            TimeSpan.FromMilliseconds(1),
            [],
            wasCancelled ? "Operation was cancelled." : null);
    }
}
