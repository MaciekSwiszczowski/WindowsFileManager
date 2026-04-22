using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class FileIdentityInterop : IFileIdentityInterop
{
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private readonly IRestartManagerInterop _restartManagerInterop;
    private readonly IShellInterop _shellInterop;

    public FileIdentityInterop(
        IRestartManagerInterop restartManagerInterop,
        IShellInterop shellInterop)
    {
        _restartManagerInterop = restartManagerInterop;
        _shellInterop = shellInterop;
    }

    public FileIdResult GetFileId(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.None);

            return GetFileIdFromHandle(stream.SafeFileHandle);
        }
        catch (Exception ex)
        {
            return new FileIdResult(false, null, ex.Message);
        }
    }

    public FileLockDiagnosticsResult GetLockDiagnostics(string path)
    {
        try
        {
            var lockBy = new List<string>();
            var lockPids = new List<int>();
            var lockServices = new List<string>();
            var errors = new List<string>();

            var inUse = TryGetRestartManagerLocks(path, lockBy, lockPids, lockServices, out var rmError);
            if (!string.IsNullOrWhiteSpace(rmError))
            {
                errors.Add(rmError);
            }

            var usageProbe = _shellInterop.TryGetFileIsInUse(path);
            if (!string.IsNullOrWhiteSpace(usageProbe.AppName))
            {
                lockBy.Add(usageProbe.AppName);
                Deduplicate(lockBy);
            }

            if (!string.IsNullOrWhiteSpace(usageProbe.ErrorMessage))
            {
                errors.Add(usageProbe.ErrorMessage);
            }

            return new FileLockDiagnosticsResult(
                success: true,
                inUse: inUse,
                lockBy: lockBy,
                lockPids: lockPids,
                lockServices: lockServices,
                usage: usageProbe.Usage,
                canSwitchTo: usageProbe.CanSwitchTo,
                canClose: usageProbe.CanClose,
                errorMessage: errors.Count > 0 ? string.Join(" | ", errors) : null);
        }
        catch (Exception ex)
        {
            return new FileLockDiagnosticsResult(
                success: false,
                inUse: null,
                lockBy: [],
                lockPids: [],
                lockServices: [],
                usage: null,
                canSwitchTo: null,
                canClose: null,
                errorMessage: ex.Message);
        }
    }

    public FileIdentityDetailsResult GetIdentityDetails(string path)
    {
        try
        {
            var fileId = GetFileId(path);
            var finalPath = Path.GetFullPath(path);

            return new FileIdentityDetailsResult(
                fileId.Success,
                fileId.FileId128,
                null,
                null,
                null,
                finalPath,
                fileId.ErrorMessage);
        }
        catch (Exception ex)
        {
            return new FileIdentityDetailsResult(false, null, null, null, null, path, ex.Message);
        }
    }

    public FileLinkDiagnosticsResult GetLinkDiagnostics(string path)
    {
        try
        {
            FileSystemInfo info = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            var linkTarget = info.LinkTarget ?? string.Empty;
            var reparseTag = info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                ? "Reparse point"
                : string.Empty;

            return new FileLinkDiagnosticsResult(
                true,
                linkTarget,
                string.IsNullOrWhiteSpace(linkTarget) ? string.Empty : "Link target reported by Windows",
                reparseTag,
                string.Empty,
                string.Empty,
                null);
        }
        catch (Exception ex)
        {
            return new FileLinkDiagnosticsResult(false, null, null, null, null, null, ex.Message);
        }
    }

    public FileStreamDiagnosticsResult GetStreamDiagnostics(string path)
    {
        return new FileStreamDiagnosticsResult(true, 0, [], null);
    }

    public FileSecurityDiagnosticsResult GetSecurityDiagnostics(string path)
    {
        return new FileSecurityDiagnosticsResult(true, null, null, null, null, null, null, null);
    }

    public FileThumbnailDiagnosticsResult GetThumbnailDiagnostics(string path)
    {
        var progId = Path.GetExtension(path);
        return new FileThumbnailDiagnosticsResult(true, null, progId, null);
    }

    private static unsafe FileIdResult GetFileIdFromHandle(
        Microsoft.Win32.SafeHandles.SafeFileHandle safeHandle)
    {
        var handle = new HANDLE(safeHandle.DangerousGetHandle());
        FILE_ID_INFO fileIdInfo;

        var success = PInvoke.GetFileInformationByHandleEx(
            handle,
            FILE_INFO_BY_HANDLE_CLASS.FileIdInfo,
            &fileIdInfo,
            (uint)sizeof(FILE_ID_INFO));

        if (!success)
        {
            var error = Marshal.GetLastPInvokeError();
            return new FileIdResult(false, null, $"GetFileInformationByHandleEx failed with error {error}");
        }

        var bytes = new byte[16];
        var ptr = (byte*)&fileIdInfo.FileId;
        new ReadOnlySpan<byte>(ptr, 16).CopyTo(bytes);

        return new FileIdResult(true, bytes, null);
    }

    private bool? TryGetRestartManagerLocks(
        string path,
        List<string> lockBy,
        List<int> lockPids,
        List<string> lockServices,
        out string? error)
    {
        error = null;
        var startResult = _restartManagerInterop.StartSession(out var sessionHandle);
        if (startResult != ErrorSuccess)
        {
            error = $"RmStartSession failed with error {startResult}";
            return null;
        }

        try
        {
            var resources = new[] { path };
            var registerResult = _restartManagerInterop.RegisterResources(sessionHandle, resources);

            if (registerResult != ErrorSuccess)
            {
                error = $"RmRegisterResources failed with error {registerResult}";
                return null;
            }

            uint processInfoNeeded = 0;
            uint processInfo = 0;
            uint rebootReasons;

            var listResult = _restartManagerInterop.GetList(
                sessionHandle,
                out processInfoNeeded,
                ref processInfo,
                null,
                out rebootReasons);

            if (listResult != ErrorSuccess && listResult != ErrorMoreData)
            {
                error = $"RmGetList failed with error {listResult}";
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
                out rebootReasons);

            if (listResult != ErrorSuccess)
            {
                error = $"RmGetList (details) failed with error {listResult}";
                return null;
            }

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
            return lockBy.Count > 0 || lockPids.Count > 0 || lockServices.Count > 0;
        }
        finally
        {
            _ = _restartManagerInterop.EndSession(sessionHandle);
        }
    }

    private static void Deduplicate<T>(List<T> source)
    {
        if (source.Count <= 1)
        {
            return;
        }

        var set = new HashSet<T>();
        var index = 0;
        foreach (var item in source)
        {
            if (set.Add(item))
            {
                source[index++] = item;
            }
        }

        if (index < source.Count)
        {
            source.RemoveRange(index, source.Count - index);
        }
    }
}
