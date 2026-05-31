using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using UiDispatcherQueuePriority = Microsoft.UI.Dispatching.DispatcherQueuePriority;
using UiDispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace WinUiFileManager.Presentation.Scheduling;

/// <summary>
/// An Rx <see cref="IScheduler"/> that marshals scheduled work onto a WinUI
/// <see cref="UiDispatcherQueue"/> (the UI thread). It is how the Presentation layer hands an Rx
/// "UI scheduler" to pipelines (e.g. <see cref="WinUiFileManager.Presentation.Services.DialogMessageOrchestrator"/>)
/// so <c>ObserveOn(scheduler)</c> resumes on the UI thread (AGENTS.md §6).
/// </summary>
/// <remarks>
/// Supports immediate, delayed (timer-backed), and periodic scheduling. Each scheduled item returns an
/// <see cref="IDisposable"/> that cancels the pending work and, for timers, detaches the
/// <c>Tick</c> handler and stops the timer so no subscription is left dangling (AGENTS.md §5).
/// <see cref="Interlocked.Exchange{T}(ref T,T)"/> guards the timer field so cancel-vs-tick races dispose
/// the timer exactly once.
/// </remarks>
internal sealed class DispatcherQueueScheduler : LocalScheduler, ISchedulerPeriodic
{
    /// <summary>Creates a scheduler at <see cref="UiDispatcherQueuePriority.Normal"/> priority.</summary>
    public DispatcherQueueScheduler(UiDispatcherQueue dispatcherQueue)
        : this(dispatcherQueue, UiDispatcherQueuePriority.Normal)
    {
    }

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dispatcherQueue"/> is null.</exception>
    public DispatcherQueueScheduler(UiDispatcherQueue dispatcherQueue, UiDispatcherQueuePriority priority)
    {
        DispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        Priority = priority;
    }

    /// <summary>The target UI dispatcher queue work is marshalled onto.</summary>
    public UiDispatcherQueue DispatcherQueue { get; }

    /// <summary>The priority at which work is enqueued.</summary>
    public UiDispatcherQueuePriority Priority { get; }

    /// <summary>Schedules <paramref name="action"/> to run as soon as possible on the dispatcher.
    /// The returned disposable cancels it if it has not started; returns a no-op disposable when the work
    /// could not be enqueued.</summary>
    public override IDisposable Schedule<TState>(
        TState state,
        Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var disposable = new SingleAssignmentDisposable();
        var queued = DispatcherQueue.TryEnqueue(Priority, () =>
        {
            // Skip if the caller disposed before the work ran.
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

    /// <summary>Schedules <paramref name="action"/> after <paramref name="dueTime"/>. A zero/negative
    /// delay runs immediately; otherwise it is backed by a dispatcher timer.</summary>
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

    /// <summary>Schedules <paramref name="action"/> to run repeatedly every <paramref name="period"/> on
    /// the dispatcher, threading the evolving state through each tick. The returned disposable stops and
    /// detaches the timer.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="period"/> is negative.</exception>
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

        void OnTick(UiDispatcherQueueTimer sender, object args) => currentState = action(currentState);
    }

    /// <summary>Timer-backed one-shot delayed schedule: fires once after <paramref name="dueTime"/>,
    /// stopping and detaching the timer on tick or on cancellation (whichever wins the
    /// <see cref="Interlocked.Exchange{T}(ref T,T)"/> race).</summary>
    private IDisposable ScheduleDelayed<TState>(
        TState state,
        TimeSpan dueTime,
        Func<IScheduler, TState, IDisposable> action)
    {
        var disposable = new MultipleAssignmentDisposable();
        UiDispatcherQueueTimer? timer = DispatcherQueue.CreateTimer();

        void OnTick(UiDispatcherQueueTimer sender, object args)
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
            if (activeTimer is not null)
            {
                activeTimer.Tick -= OnTick;
                activeTimer.Stop();
            }
        });

        return disposable;
    }
}
