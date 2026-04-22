using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class ShellInterop : IShellInterop
{
    private const int ErrorSuccess = 0;
    private const uint OfCapCanSwitchTo = 0x0001;
    private const uint OfCapCanClose = 0x0002;
    private const uint SeeMaskInvokeIdList = 0x0000000C;

    public bool ShowObjectProperties(string objectName, out int lastError)
    {
        var result = ShowObjectPropertiesCore(static path => PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, null), objectName);
        lastError = result ? 0 : Marshal.GetLastWin32Error();
        return result;
    }

    public bool TryInitializeStaCom()
    {
        return TryInitializeStaComCore(InitializeStaCom);
    }

    public void UninitializeCom()
    {
        PInvoke.CoUninitialize();
    }

    public ShellExecutePropertiesResult ExecutePropertiesVerb(string objectName)
    {
        return ExecutePropertiesVerbCore(ExecutePropertiesVerbNative, objectName);
    }

    public FileIsInUseProbeResult TryGetFileIsInUse(string path)
    {
        return TryGetFileIsInUseCore(
            CreateFileIsInUseAdapter,
            Marshal.FinalReleaseComObject,
            static () => Thread.CurrentThread.GetApartmentState(),
            path);
    }

    internal static bool ShowObjectPropertiesCore(
        Func<string, bool> showObjectProperties,
        string objectName)
    {
        return showObjectProperties(objectName);
    }

    internal static bool TryInitializeStaComCore(Func<int> initializeStaCom)
    {
        var hr = initializeStaCom();
        return hr is 0 or 1;
    }

    internal static ShellExecutePropertiesResult ExecutePropertiesVerbCore(
        Func<string, ShellExecutePropertiesResult> executePropertiesVerb,
        string objectName)
    {
        return executePropertiesVerb(objectName);
    }

    internal static FileIsInUseProbeResult TryGetFileIsInUseCore(
        Func<string, (int HResult, IFileIsInUseAdapter? FileIsInUse)> fileIsInUseFactory,
        Func<object, int> finalReleaseComObject,
        Func<ApartmentState> apartmentStateProvider,
        string path)
    {
        Debug.Assert(
            apartmentStateProvider() == ApartmentState.STA,
            "TryGetFileIsInUse requires the shell COM call site to run on an STA-initialized thread.");

        var (hr, fileIsInUse) = fileIsInUseFactory(path);
        if (hr != ErrorSuccess || fileIsInUse is null)
        {
            return new FileIsInUseProbeResult(hr, null, null, null, null, null);
        }

        try
        {
            string? appName = null;
            string? usage = null;
            bool? canSwitchTo = null;
            bool? canClose = null;

            var appNameHr = fileIsInUse.GetAppName(out var appNamePtr);
            if (appNameHr == ErrorSuccess && appNamePtr != IntPtr.Zero)
            {
                appName = Marshal.PtrToStringUni(appNamePtr);
                Marshal.FreeCoTaskMem(appNamePtr);
            }

            var usageHr = fileIsInUse.GetUsage(out var usageValue);
            if (usageHr == ErrorSuccess)
            {
                usage = usageValue switch
                {
                    0 => "Playing",
                    1 => "Editing",
                    2 => "Generic",
                    _ => usageValue.ToString()
                };
            }

            var capabilitiesHr = fileIsInUse.GetCapabilities(out var capabilities);
            if (capabilitiesHr == ErrorSuccess)
            {
                canSwitchTo = (capabilities & OfCapCanSwitchTo) != 0;
                canClose = (capabilities & OfCapCanClose) != 0;
            }

            return new FileIsInUseProbeResult(hr, appName, usage, canSwitchTo, canClose, null);
        }
        catch (Exception ex)
        {
            return new FileIsInUseProbeResult(hr, null, null, null, null, ex.Message);
        }
        finally
        {
            _ = finalReleaseComObject(fileIsInUse.InnerObject);
        }
    }

    private static unsafe int InitializeStaCom()
    {
        return (int)PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);
    }

    private static unsafe ShellExecutePropertiesResult ExecutePropertiesVerbNative(string path)
    {
        fixed (char* verb = "properties")
        fixed (char* file = path)
        {
            var executeInfo = new SHELLEXECUTEINFOW
            {
                cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                fMask = SeeMaskInvokeIdList,
                hwnd = HWND.Null,
                lpVerb = verb,
                lpFile = file,
                nShow = 5
            };

            var success = PInvoke.ShellExecuteEx(ref executeInfo);
            return new ShellExecutePropertiesResult(
                success,
                success ? 0 : Marshal.GetLastWin32Error(),
                (nint)executeInfo.hInstApp.Value);
        }
    }

    private static (int HResult, IFileIsInUseAdapter? FileIsInUse) CreateFileIsInUseAdapter(string path)
    {
        var iid = typeof(IFileIsInUseNative).GUID;
        var hr = (int)PInvoke.SHCreateItemFromParsingName(path, null, iid, out object? fileIsInUse);
        return (hr, fileIsInUse is IFileIsInUseNative native ? new GeneratedFileIsInUseAdapter(native) : null);
    }

    internal interface IFileIsInUseAdapter
    {
        object InnerObject { get; }

        int GetAppName(out IntPtr appName);

        int GetUsage(out int usage);

        int GetCapabilities(out uint capabilities);
    }

    private sealed class GeneratedFileIsInUseAdapter : IFileIsInUseAdapter
    {
        private readonly IFileIsInUseNative _inner;

        internal GeneratedFileIsInUseAdapter(IFileIsInUseNative inner)
        {
            _inner = inner;
        }

        public object InnerObject => _inner;

        public int GetAppName(out IntPtr appName) => _inner.GetAppName(out appName);

        public int GetUsage(out int usage)
        {
            var result = _inner.GetUsage(out var fileUsage);
            usage = (int)fileUsage;
            return result;
        }

        public int GetCapabilities(out uint capabilities) => _inner.GetCapabilities(out capabilities);
    }

    [ComImport]
    [Guid("64a1cbf0-3a1a-4461-9158-376969693950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileIsInUseNative
    {
        [PreserveSig]
        int GetAppName(out IntPtr appName);

        [PreserveSig]
        int GetUsage(out FileUsage usage);

        [PreserveSig]
        int GetCapabilities(out uint capabilities);

        [PreserveSig]
        int GetSwitchToHWND(out IntPtr hwnd);

        [PreserveSig]
        int CloseFile();
    }

    private enum FileUsage
    {
        Playing = 0,
        Editing = 1,
        Generic = 2
    }
}
