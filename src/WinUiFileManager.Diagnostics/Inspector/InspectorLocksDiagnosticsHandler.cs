using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

public sealed class InspectorLocksDiagnosticsHandler : IDisposable
{
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

    public void Initialize()
    {
        _messenger.Register<InspectorLocksDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

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
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector lock diagnostics for {Path}", message.Path.DisplayPath);
            return FileLockDiagnostics.None;
        }
    }

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

            uint processInfo = 0;
            var listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out var processInfoNeeded,
                ref processInfo,
                processInfos: null,
                out _);

            if (listResult != ErrorSuccess && listResult != ErrorMoreData)
            {
                return null;
            }

            if (processInfoNeeded == 0)
            {
                return false;
            }

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
            _ = _restartManagerInterop.EndSession(sessionHandle);
        }
    }

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
