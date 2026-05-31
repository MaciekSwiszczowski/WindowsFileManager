using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorLocksDiagnosticsRequestMessage"/> by asking
/// the Windows Restart Manager which processes/services currently hold a handle to the requested path.
/// </summary>
/// <remarks>
/// Lifetime: DI singleton; registers in <see cref="Initialize"/>, unregisters in <see cref="Dispose"/>,
/// which is effectively unreachable because the container is never disposed (AGENTS.md §5).
/// Threading: answered via <c>message.Reply(Task.Run(...))</c>; <see cref="Load"/> runs on the thread pool
/// under <see cref="LoadTimeout"/>. The Restart Manager <i>session</i> is a native OS resource; here it is
/// opened and released with a manual <c>try/finally</c> around <c>EndSession</c> rather than an
/// <see cref="IDisposable"/> wrapper.
/// </remarks>
public sealed class InspectorLocksDiagnosticsHandler : IDisposable
{
    // Win32 error codes from the Restart Manager API.
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<InspectorLocksDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private readonly IRestartManagerInterop _restartManagerInterop;
    private bool _disposed;

    public InspectorLocksDiagnosticsHandler(
        IMessenger messenger,
        IRestartManagerInterop restartManagerInterop,
        ILogger<InspectorLocksDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _restartManagerInterop = restartManagerInterop;
        _logger = logger;
    }

    /// <summary>Registers the request handler. Not idempotent — call exactly once (AGENTS.md §4).</summary>
    public void Initialize()
    {
        _messenger.Register<InspectorLocksDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    /// <summary>Unregisters from the messenger (idempotent). See type remarks: effectively never called.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Determines whether the path is locked and by whom.
    /// </summary>
    /// <param name="message">The request carrying the target path and cancellation token.</param>
    /// <returns>
    /// Lock diagnostics (in-use flag is <see langword="null"/> when the Restart Manager could not be
    /// queried), or <see cref="FileLockDiagnostics.None"/> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Real cancellation is rethrown; other errors are logged and degraded to None.</remarks>
    private FileLockDiagnostics Load(InspectorLocksDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var lockBy = new List<string>();
            var lockPids = new List<int>();
            var lockServices = new List<string>();
            var inUse = TryGetRestartManagerLocks(message.Path.DisplayPath, lockBy, lockPids, lockServices);

            return new FileLockDiagnostics(
                inUse: inUse,
                lockBy: lockBy,
                lockPids: lockPids,
                lockServices: lockServices);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Rethrow only for genuine caller cancellation; timeout cancellation degrades to None below.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector lock diagnostics for {Path}", message.Path.DisplayPath);
            return FileLockDiagnostics.None;
        }
    }

    /// <summary>
    /// Runs a Restart Manager session against the path and fills the lock lists.
    /// </summary>
    /// <param name="path">The resource path to register.</param>
    /// <param name="lockBy">Receives the locking app/process display names.</param>
    /// <param name="lockPids">Receives the locking process ids.</param>
    /// <param name="lockServices">Receives the locking service short names.</param>
    /// <returns><see langword="true"/>/<see langword="false"/> for locked/not-locked, or <see langword="null"/>
    /// if the Restart Manager could not be queried.</returns>
    /// <remarks>
    /// The session handle is always released in the <c>finally</c>. <c>RmGetList</c> uses the standard
    /// two-call pattern: the first call (with a null buffer) returns the required count (and
    /// <see cref="ErrorMoreData"/>), then a buffer of that size is allocated and the call repeated.
    /// </remarks>
    private bool? TryGetRestartManagerLocks(
        string path,
        List<string> lockBy,
        List<int> lockPids,
        List<string> lockServices)
    {
        var startResult = _restartManagerInterop.StartSession(out var sessionHandle);
        if (startResult != ErrorSuccess)
        {
            return null;
        }

        try
        {
            var registerResult = _restartManagerInterop.RegisterResources(sessionHandle, [path]);
            if (registerResult != ErrorSuccess)
            {
                return null;
            }

            // First call: probe how many process-info records are needed (null buffer).
            uint processInfo = 0;
            var listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out var processInfoNeeded,
                ref processInfo,
                processInfos: null,
                out _);

            // ERROR_MORE_DATA is expected here (the buffer was empty); only other failures abort.
            if (listResult != ErrorSuccess && listResult != ErrorMoreData)
            {
                return null;
            }

            if (processInfoNeeded == 0)
            {
                return false;
            }

            // Second call: allocate to the reported size and fetch the records.
            processInfo = processInfoNeeded;
            var processInfos = new RestartManagerProcessInfo[processInfoNeeded];
            listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                processInfos,
                out _);

            if (listResult != ErrorSuccess)
            {
                return null;
            }

            AddLocks(processInfos, processInfo, lockBy, lockPids, lockServices);
            return lockBy.Count > 0 || lockPids.Count > 0 || lockServices.Count > 0;
        }
        finally
        {
            // Always release the native Restart Manager session, even on early-return failures.
            _ = _restartManagerInterop.EndSession(sessionHandle);
        }
    }

    /// <summary>
    /// Projects Restart Manager process records into the lock lists, falling back to "PID n" when an app
    /// name is missing, then removes duplicates.
    /// </summary>
    private static void AddLocks(
        IReadOnlyList<RestartManagerProcessInfo> processInfos,
        uint processInfo,
        List<string> lockBy,
        List<int> lockPids,
        List<string> lockServices)
    {
        for (var i = 0; i < processInfo; i++)
        {
            var processInfoItem = processInfos[i];
            var appName = string.IsNullOrWhiteSpace(processInfoItem.AppName)
                ? $"PID {processInfoItem.ProcessId}"
                : processInfoItem.AppName.Trim();

            lockBy.Add(appName);

            if (processInfoItem.ProcessId > 0)
            {
                lockPids.Add(processInfoItem.ProcessId);
            }

            if (!string.IsNullOrWhiteSpace(processInfoItem.ServiceShortName))
            {
                lockServices.Add(processInfoItem.ServiceShortName.Trim());
            }
        }

        Deduplicate(lockBy);
        Deduplicate(lockPids);
        Deduplicate(lockServices);
    }

    /// <summary>Removes duplicate entries in place while preserving first-occurrence order.</summary>
    private static void Deduplicate<T>(List<T> source)
    {
        if (source.Count <= 1)
        {
            return;
        }

        var seen = new HashSet<T>();
        for (var i = source.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(source[i]))
            {
                source.RemoveAt(i);
            }
        }
    }
}
