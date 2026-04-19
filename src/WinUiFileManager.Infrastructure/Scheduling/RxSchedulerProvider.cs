using System.Reactive.Concurrency;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Infrastructure.Scheduling;

/// <summary>
/// Default production scheduler provider. The background scheduler is the
/// shared task-pool scheduler; the main-thread scheduler is captured at
/// construction time from the current <see cref="SynchronizationContext"/>
/// (i.e. the UI dispatcher when the application starts up).
/// </summary>
public sealed class RxSchedulerProvider : ISchedulerProvider
{
    public RxSchedulerProvider()
    {
        Background = TaskPoolScheduler.Default;
        var uiContext = SynchronizationContext.Current;
        MainThread = uiContext is not null
            ? new SynchronizationContextScheduler(uiContext)
            : CurrentThreadScheduler.Instance;
    }

    public IScheduler Background { get; }

    public IScheduler MainThread { get; }
}
