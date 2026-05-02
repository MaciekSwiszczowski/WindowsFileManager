using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Microsoft.UI.Dispatching;

namespace WinUiFileManager.TestApp;

internal sealed class DispatcherQueueScheduler : LocalScheduler, ISchedulerPeriodic
{
    public DispatcherQueueScheduler(DispatcherQueue dispatcherQueue)
        : this(dispatcherQueue, DispatcherQueuePriority.Normal)
    {
    }

    public DispatcherQueueScheduler(DispatcherQueue dispatcherQueue, DispatcherQueuePriority priority)
    {
        DispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        Priority = priority;
    }

    public DispatcherQueue DispatcherQueue { get; }

    public DispatcherQueuePriority Priority { get; }

    public override IDisposable Schedule<TState>(
        TState state,
        Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var disposable = new SingleAssignmentDisposable();
        var queued = DispatcherQueue.TryEnqueue(Priority, () =>
        {
            if (!disposable.IsDisposed)
            {
                disposable.Disposable = action(this, state);
            }
        });

        if (!queued)
        {
            return Disposable.Empty;
        }

        return disposable;
    }

    public override IDisposable Schedule<TState>(
        TState state,
        TimeSpan dueTime,
        Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var normalizedDueTime = Scheduler.Normalize(dueTime);
        return normalizedDueTime == TimeSpan.Zero
            ? Schedule(state, action)
            : ScheduleDelayed(state, normalizedDueTime, action);
    }

    public IDisposable SchedulePeriodic<TState>(
        TState state,
        TimeSpan period,
        Func<TState, TState> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfLessThan(period, TimeSpan.Zero);

        var timer = DispatcherQueue.CreateTimer();
        var currentState = state;

        timer.Interval = period;
        timer.IsRepeating = true;
        timer.Tick += OnTick;
        timer.Start();

        return Disposable.Create(() =>
        {
            var activeTimer = Interlocked.Exchange(ref timer, null);

            activeTimer.Tick -= OnTick;
            activeTimer.Stop();
        });

        void OnTick(DispatcherQueueTimer sender, object args) => currentState = action(currentState);
    }

    private IDisposable ScheduleDelayed<TState>(
        TState state,
        TimeSpan dueTime,
        Func<IScheduler, TState, IDisposable> action)
    {
        var disposable = new MultipleAssignmentDisposable();
        DispatcherQueueTimer? timer = DispatcherQueue.CreateTimer();

        void OnTick(DispatcherQueueTimer sender, object args)
        {
            var activeTimer = Interlocked.Exchange(ref timer, null);
            if (activeTimer is null)
            {
                return;
            }

            activeTimer.Tick -= OnTick;
            activeTimer.Stop();

            if (!disposable.IsDisposed)
            {
                disposable.Disposable = action(this, state);
            }
        }

        timer.Interval = dueTime;
        timer.IsRepeating = false;
        timer.Tick += OnTick;
        timer.Start();

        disposable.Disposable = Disposable.Create(() =>
        {
            var activeTimer = Interlocked.Exchange(ref timer, null);
            if (activeTimer is null)
            {
                return;
            }

            activeTimer.Tick -= OnTick;
            activeTimer.Stop();
        });

        return disposable;
    }
}
