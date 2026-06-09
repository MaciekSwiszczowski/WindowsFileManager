using CommunityToolkit.Mvvm.Messaging;
using R3;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Presentation.Messaging;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class InspectorInitializationViewModelTests
{
    [Fact]
    public void DeferredSelectionObservable_DebouncesWithInjectedTimeProvider()
    {
        var messenger = new FileManagerMessenger(new StrongReferenceMessenger());
        var activePanels = new FakeActivePanelsService();
        var timeProvider = new ManualTimeProvider();
        var sut = CreateSut(activePanels, messenger, timeProvider);
        var received = new List<FileListingRow>();

        using var subscription = sut.DeferredSelectionObservable.Subscribe(received.Add);

        var first = File("first.txt");
        var second = File("second.txt");
        messenger.Send(new FileTableSelectionChangedMessage("Left", [first], IsParentRowSelected: false, first));
        timeProvider.Advance(TimeSpan.FromMilliseconds(299));

        Assert.Empty(received);

        messenger.Send(new FileTableSelectionChangedMessage("Left", [second], IsParentRowSelected: false, second));
        timeProvider.Advance(TimeSpan.FromMilliseconds(299));

        Assert.Empty(received);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Single(received);
        Assert.True(ReferenceEquals(received[0], second));
    }

    private static InspectorInitializationViewModel CreateSut(
        IActivePanelsService activePanelsService,
        IFileManagerMessenger messenger,
        TimeProvider timeProvider)
    {
        return new InspectorInitializationViewModel(
            activePanelsService,
            new ImmediateSynchronizationContext(),
            timeProvider,
            messenger,
            static category => new InspectorCategoryViewModel(category, FakeInspectorDiagnosticsGate.Instance),
            static request => new InspectorBasicFieldViewModel(request),
            static request => new InspectorThumbnailFieldViewModel(request),
            static request => new InspectorToggleFieldViewModel(request));
    }

    private static FileListingRow File(string name) =>
        new(new FileSystemEntryModel(
            NormalizedPath.FromUserInput(@"C:\Temp"),
            name,
            Path.GetExtension(name),
            ItemKind.File,
            1,
            DateTime.Today,
            DateTime.Today,
            FileAttributes.Normal));

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
            callback(state);
        }

        public override void Send(SendOrPostCallback callback, object? state)
        {
            callback(state);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _utcNow.UtcTicks;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan delta)
        {
            var target = _utcNow + delta;
            while (TryGetNextDueTimer(target, out var timer))
            {
                _utcNow = timer.DueAt;
                timer.Invoke();
            }

            _utcNow = target;
        }

        private bool TryGetNextDueTimer(DateTimeOffset target, out ManualTimer timer)
        {
            ManualTimer? next = null;
            foreach (var candidate in _timers)
            {
                if (!candidate.IsDueAtOrBefore(target))
                {
                    continue;
                }

                if (next is null || candidate.DueAt < next.DueAt)
                {
                    next = candidate;
                }
            }

            if (next is null)
            {
                timer = ManualTimer.Disabled;
                return false;
            }

            timer = next;
            return true;
        }

        private void Remove(ManualTimer timer)
        {
            _timers.Remove(timer);
        }

        private sealed class ManualTimer : ITimer
        {
            public static readonly ManualTimer Disabled = new();

            private readonly ManualTimeProvider? _owner;
            private readonly TimerCallback? _callback;
            private readonly object? _state;
            private TimeSpan _period;
            private bool _disposed;

            private ManualTimer()
            {
            }

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? DateTimeOffset.MaxValue
                    : owner.GetUtcNow() + dueTime;
            }

            public DateTimeOffset DueAt { get; private set; } = DateTimeOffset.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed || _owner is null)
                {
                    return false;
                }

                _period = period;
                DueAt = dueTime == Timeout.InfiniteTimeSpan
                    ? DateTimeOffset.MaxValue
                    : _owner.GetUtcNow() + dueTime;
                return true;
            }

            public bool IsDueAtOrBefore(DateTimeOffset target) => !_disposed && DueAt <= target;

            public void Invoke()
            {
                if (_disposed || _owner is null || _callback is null)
                {
                    return;
                }

                if (_period == Timeout.InfiniteTimeSpan)
                {
                    DueAt = DateTimeOffset.MaxValue;
                }
                else
                {
                    DueAt += _period;
                }

                _callback(_state);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DueAt = DateTimeOffset.MaxValue;
                _owner?.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
