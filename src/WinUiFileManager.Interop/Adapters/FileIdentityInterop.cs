#pragma warning disable RS0030 // Legacy DllImport declarations stay quarantined here until the CsWin32 migration batch replaces them.
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public sealed class FileIdentityInterop : IFileIdentityInterop
{
    private const int ErrorSuccess = 0;
    private const int ErrorMoreData = 234;
    private const int CchRmSessionKey = 33;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const uint OfCapCanSwitchTo = 0x0001;
    private const uint OfCapCanClose = 0x0002;

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

            var usage = TryGetFileIsInUse(path, out var fileIsInUseAppName, out var canSwitchTo, out var canClose, out var usageError);
            if (!string.IsNullOrWhiteSpace(fileIsInUseAppName))
            {
                lockBy.Add(fileIsInUseAppName);
                Deduplicate(lockBy);
            }

            if (!string.IsNullOrWhiteSpace(usageError))
            {
                errors.Add(usageError);
            }

            return new FileLockDiagnosticsResult(
                success: true,
                inUse: inUse,
                lockBy: lockBy,
                lockPids: lockPids,
                lockServices: lockServices,
                usage: usage,
                canSwitchTo: canSwitchTo,
                canClose: canClose,
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

    private static bool? TryGetRestartManagerLocks(
        string path,
        List<string> lockBy,
        List<int> lockPids,
        List<string> lockServices,
        out string? error)
    {
        error = null;
        var sessionKey = new StringBuilder(CchRmSessionKey);
        var startResult = RmStartSession(out var sessionHandle, 0, sessionKey);
        if (startResult != ErrorSuccess)
        {
            error = $"RmStartSession failed with error {startResult}";
            return null;
        }

        try
        {
            var resources = new[] { path };
            var registerResult = RmRegisterResources(
                sessionHandle,
                (uint)resources.Length,
                resources,
                0,
                null,
                0,
                null);

            if (registerResult != ErrorSuccess)
            {
                error = $"RmRegisterResources failed with error {registerResult}";
                return null;
            }

            uint processInfoNeeded = 0;
            uint processInfo = 0;
            uint rebootReasons;

            var listResult = RmGetList(
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
            var processInfos = new RmProcessInfo[processInfoNeeded];
            listResult = RmGetList(
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
                    ? $"PID {processInfoItem.Process.ProcessId}"
                    : processInfoItem.AppName.Trim();

                lockBy.Add(appName);

                if (processInfoItem.Process.ProcessId > 0)
                {
                    lockPids.Add(processInfoItem.Process.ProcessId);
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
            _ = RmEndSession(sessionHandle);
        }
    }

    private static string? TryGetFileIsInUse(
        string path,
        out string? appName,
        out bool? canSwitchTo,
        out bool? canClose,
        out string? error)
    {
        appName = null;
        canSwitchTo = null;
        canClose = null;
        error = null;

        var iid = typeof(IFileIsInUseNative).GUID;
        var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var fileIsInUse);

        // For many items this interface is unavailable; treat that as best-effort no-data.
        if (hr != ErrorSuccess || fileIsInUse is null)
        {
            return null;
        }

        try
        {
            string? usageText = null;
            var appNameHr = fileIsInUse.GetAppName(out var appNamePtr);
            if (appNameHr == ErrorSuccess && appNamePtr != IntPtr.Zero)
            {
                appName = Marshal.PtrToStringUni(appNamePtr);
                Marshal.FreeCoTaskMem(appNamePtr);
            }

            var usageHr = fileIsInUse.GetUsage(out var usage);
            if (usageHr == ErrorSuccess)
            {
                usageText = usage switch
                {
                    FileUsageType.Playing => "Playing",
                    FileUsageType.Editing => "Editing",
                    FileUsageType.Generic => "Generic",
                    _ => usage.ToString()
                };
            }

            var capHr = fileIsInUse.GetCapabilities(out var capabilities);
            if (capHr == ErrorSuccess)
            {
                canSwitchTo = (capabilities & OfCapCanSwitchTo) != 0;
                canClose = (capabilities & OfCapCanClose) != 0;
            }

            return usageText;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
        finally
        {
            if (fileIsInUse is not null)
            {
                _ = Marshal.ReleaseComObject(fileIsInUse);
            }
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

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RmStartSession(
        out uint pSessionHandle,
        int dwSessionFlags,
        StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        [In] string[]? rgsFileNames,
        uint nApplications,
        [In] RmUniqueProcess[]? rgApplications,
        uint nServices,
        [In] string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", SetLastError = true)]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RmProcessInfo[]? rgAffectedApps,
        out uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll", SetLastError = true)]
    private static extern int RmEndSession(uint dwSessionHandle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IFileIsInUseNative? ppv);

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RmAppType
    {
        Unknown = 0,
        MainWindow = 1,
        OtherWindow = 2,
        Service = 3,
        Explorer = 4,
        Console = 5,
        Critical = 1000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string ServiceShortName;

        public RmAppType ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Restartable;
    }

    private enum FileUsageType
    {
        Playing = 0,
        Editing = 1,
        Generic = 2
    }

    [ComImport]
    [Guid("64a1cbf0-3a1a-4461-9158-376969693950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileIsInUseNative
    {
        [PreserveSig]
        int GetAppName(out IntPtr appName);

        [PreserveSig]
        int GetUsage(out FileUsageType usage);

        [PreserveSig]
        int GetCapabilities(out uint capabilities);

        [PreserveSig]
        int GetSwitchToHWND(out IntPtr hwnd);

        [PreserveSig]
        int CloseFile();
    }
}
