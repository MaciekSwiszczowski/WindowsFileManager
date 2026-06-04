using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Diagnostics.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> by asking
/// the Windows Restart Manager which processes/services currently hold a handle to the requested path.
/// </summary>
/// <remarks>
/// The Restart Manager <i>session</i> is a native OS resource; here it is opened and released with a manual
/// <c>try/finally</c> around <c>EndSession</c> rather than an <see cref="IDisposable"/> wrapper.
/// </remarks>
public sealed class InspectorLocksDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        FileLockDiagnostics,
        InspectorLocksDiagnosticsResponseMessage>
{
    // Win32 error codes from the Restart Manager API.
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private const int InitialProcessInfoCapacity = 8;

    private readonly IRestartManagerInterop _restartManagerInterop;

    public InspectorLocksDiagnosticsHandler(
        IMessenger messenger,
        IRestartManagerInterop restartManagerInterop,
        ILogger<InspectorLocksDiagnosticsHandler> logger,
        Func<FileLockDiagnostics, InspectorLocksDiagnosticsResponseMessage> responseFactory)
        : base(messenger, logger, responseFactory)
    {
        _restartManagerInterop = restartManagerInterop;
    }

    /// <summary>
    /// Determines whether the path is locked and by whom.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>
    /// Lock diagnostics (in-use flag is <see langword="null"/> when the Restart Manager could not be
    /// queried), or <see cref="FileLockDiagnostics.None"/> on failure.
    /// </returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to None by the base class.</remarks>
    protected override Task<FileLockDiagnostics> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        if (CanOpenFileExclusively(message.Path.DisplayPath))
        {
            return Task.FromResult(new FileLockDiagnostics(
                inUse: false,
                lockBy: [],
                lockPids: [],
                lockServices: []));
        }

        var lockBy = new List<string>();
        var lockPids = new List<int>();
        var lockServices = new List<string>();
        var inUse = TryGetRestartManagerLocks(message.Path.DisplayPath, lockBy, lockPids, lockServices);

        return Task.FromResult(new FileLockDiagnostics(
            inUse: inUse,
            lockBy: lockBy,
            lockPids: lockPids,
            lockServices: lockServices));
    }

    protected override FileLockDiagnostics GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileLockDiagnostics.None;

    /// <summary>
    /// Checks the cheap, definitive negative case before paying for a Restart Manager session.
    /// </summary>
    /// <remarks>
    /// A successful <see cref="FileShare.None"/> open proves that no current handle conflicts with exclusive
    /// access to this file, so Restart Manager cannot add useful lock-owner data. Failures are treated as
    /// "unknown" rather than "locked": access-denied, cloud, or transient file-system states still fall through
    /// to Restart Manager, which remains the authoritative owner lookup.
    /// </remarks>
    private static bool CanOpenFileExclusively(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
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
    /// The session handle is always released in the <c>finally</c>. <c>RmGetList</c> is first called with a
    /// small buffer because the usual lock-owner count is zero or one; only <see cref="ErrorMoreData"/> falls
    /// back to allocating the exact OS-reported size and retrying.
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

            var processInfos = new RestartManagerProcessInfo[InitialProcessInfoCapacity];
            var processInfo = (uint)processInfos.Length;
            var listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out var processInfoNeeded,
                ref processInfo,
                processInfos,
                out _);

            if (listResult == ErrorSuccess)
            {
                AddLocks(processInfos, processInfo, lockBy, lockPids, lockServices);
                return lockBy.Count > 0 || lockPids.Count > 0 || lockServices.Count > 0;
            }

            if (listResult != ErrorMoreData || processInfoNeeded == 0)
            {
                return null;
            }

            // Retry only when the common small buffer was not enough.
            processInfo = processInfoNeeded;
            processInfos = new RestartManagerProcessInfo[processInfoNeeded];
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
