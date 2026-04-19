using System.Reactive.Concurrency;

namespace WinUiFileManager.Application.Abstractions;

/// <summary>
/// Exposes the schedulers used by Rx pipelines across the application.
/// Tests inject a TestScheduler for both properties to virtualize time; the
/// production implementation returns a background task-pool scheduler and a
/// UI-bound scheduler captured from the application entry point.
/// </summary>
public interface ISchedulerProvider
{
    IScheduler Background { get; }

    IScheduler MainThread { get; }
}
