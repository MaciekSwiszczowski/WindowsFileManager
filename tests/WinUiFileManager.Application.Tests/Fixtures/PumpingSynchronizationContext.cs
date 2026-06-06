using System.Collections.Concurrent;

namespace WinUiFileManager.Application.Tests.Fixtures;

/// <summary>
/// A single-thread <see cref="SynchronizationContext"/> for behavioral tests. Queued callbacks run on one
/// dedicated background thread, so a component that marshals work through it — e.g. the file-listing data
/// source's <c>ObserveOn(uiSynchronizationContext)</c> — has a single, deterministic writer thread (satisfying
/// the row store's single-writer contract). Tests read the component's state back through <see cref="Invoke{T}"/>
/// so reads happen on that same thread and never race the writer.
/// </summary>
internal sealed class PumpingSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;

    public PumpingSynchronizationContext()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "FileListingTestPump" };
        _thread.Start();
    }

    /// <summary>Queues <paramref name="d"/> to run on the pump thread. Safe to call after disposal (dropped),
    /// so a late watcher event racing teardown does not throw on a pool thread.</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        try
        {
            _queue.Add((d, state));
        }
        catch (InvalidOperationException)
        {
            // Pump disposed (adding completed); drop late callbacks rather than fault the caller.
        }
    }

    /// <summary>Runs <paramref name="d"/> on the pump thread and blocks until it finishes, surfacing exceptions.
    /// Runs inline when already on the pump thread (no deadlock on reentry).</summary>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Thread.CurrentThread == _thread)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim(initialState: false);
        Exception? failure = null;
        Post(
            _ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    // ReSharper disable once AccessToDisposedClosure
                    done.Set();
                }
            },
            null);
        done.Wait();
        if (failure is not null)
        {
            throw failure;
        }
    }

    /// <summary>Evaluates <paramref name="func"/> on the pump thread and returns its result — the safe way for a
    /// test to read writer-thread-owned state (e.g. the data source's <c>Items</c>).</summary>
    public T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Send(_ => result = func(), null);
        return result;
    }

    private void Run()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join();
        _queue.Dispose();
    }
}
