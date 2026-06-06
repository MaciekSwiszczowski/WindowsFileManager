using System.Diagnostics;
using R3;

namespace WinUiFileManager.Presentation.Scheduling;

/// <summary>
/// Initializes R3's process-wide observable system with WinUI dispatcher-backed time and frame providers.
/// </summary>
/// <remarks>
    /// Initializing the provider at application startup keeps time-based operators UI-aware without exposing
    /// R3 platform setup to view models or table data-source classes.
/// </remarks>
public static class R3WinUiObservableSystem
{
    private static int Initialized;

    /// <summary>
    /// Configures R3's default providers once. Later calls are ignored so tests or app startup cannot
    /// accidentally replace the process-wide observable system after subscriptions exist.
    /// </summary>
    /// <param name="unhandledExceptionHandler">Optional sink for unhandled R3 observer exceptions.</param>
    /// <remarks>
    /// Must be called on the UI thread: the WinUI provider captures the current thread's
    /// <c>DispatcherQueue</c> for its time and frame providers. If setup throws, the one-shot guard is
    /// released so a later, correct call can retry.
    /// </remarks>
    public static void Initialize(Action<Exception>? unhandledExceptionHandler = null)
    {
        if (Interlocked.Exchange(ref Initialized, 1) != 0)
        {
            return;
        }

        try
        {
            var handler = unhandledExceptionHandler ?? (static exception => Trace.WriteLine(exception.ToString()));
            WinUI3ProviderInitializer.SetDefaultObservableSystem(handler);
        }
        catch
        {
            // Failed setup left no providers installed; allow a subsequent call to try again.
            Interlocked.Exchange(ref Initialized, 0);
            throw;
        }
    }
}
