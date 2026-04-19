using System.Reactive.Concurrency;
using Microsoft.Reactive.Testing;

namespace WinUiFileManager.Application.Tests.Fakes;

/// <summary>
/// Returns the same <see cref="TestScheduler"/> for both <see cref="ISchedulerProvider.Background"/>
/// and <see cref="ISchedulerProvider.MainThread"/>, so a test can drive the entire
/// pane Rx pipeline with virtual time.
/// </summary>
public sealed class TestSchedulerProvider : ISchedulerProvider
{
    public TestSchedulerProvider(TestScheduler scheduler)
    {
        Scheduler = scheduler;
    }

    public TestScheduler Scheduler { get; }

    public IScheduler Background => Scheduler;

    public IScheduler MainThread => Scheduler;
}
