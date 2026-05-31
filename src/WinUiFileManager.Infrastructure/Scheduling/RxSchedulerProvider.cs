// RS0030 (banned-symbol) is suppressed because capturing SynchronizationContext.Current is normally discouraged,
// but this is the single approved place to capture the startup (UI) SynchronizationContext for the whole app.
#pragma warning disable RS0030 // RxSchedulerProvider is the one approved place to capture the startup SynchronizationContext.
using System.Reactive.Concurrency;
using WinUiFileManager.Application.Abstractions;

namespace WinUiFileManager.Infrastructure.Scheduling;

/// <summary>
/// Default production scheduler provider. The background scheduler is the
/// shared task-pool scheduler; the main-thread scheduler is captured at
/// construction time from the current <see cref="SynchronizationContext"/>
/// (i.e. the UI dispatcher when the application starts up). Infrastructure implementation of
/// <see cref="ISchedulerProvider"/>.
/// </summary>
/// <remarks>
/// CONSTRUCTION-TIME AFFINITY: this type MUST be constructed on the UI thread during startup so that
/// <see cref="SynchronizationContext.Current"/> is the UI dispatcher; the captured context is then used for the
/// life of the (singleton) provider. If constructed off the UI thread (no current context), it falls back to
/// <see cref="CurrentThreadScheduler.Instance"/>, which would NOT marshal work to the UI thread — a latent bug if
/// the registration/composition order ever changes.
/// </remarks>
internal sealed class RxSchedulerProvider : ISchedulerProvider
{
    public RxSchedulerProvider()
    {
        Background = TaskPoolScheduler.Default;
        var uiContext = SynchronizationContext.Current;
        // Fallback to the current-thread scheduler when there is no SynchronizationContext (e.g. constructed on a
        // pool thread / in tests). This preserves functionality but does not provide true UI-thread marshalling.
        MainThread = uiContext is not null
            ? new SynchronizationContextScheduler(uiContext)
            : CurrentThreadScheduler.Instance;
    }

    /// <summary>Scheduler for off-UI background work (shared task-pool scheduler).</summary>
    public IScheduler Background { get; }

    /// <summary>Scheduler that marshals work onto the UI thread captured at construction time.</summary>
    public IScheduler MainThread { get; }
}
