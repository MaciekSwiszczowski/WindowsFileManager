using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class ShellInterop : IShellInterop
{
    private const uint SeeMaskInvokeIdList = 0x0000000C;

    public bool ShowObjectProperties(string objectName, out int lastError)
    {
        var result = ShowObjectPropertiesCore(static path => PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path), objectName);
        lastError = result ? 0 : Marshal.GetLastWin32Error();
        return result;
    }

    public bool TryInitializeStaCom()
        => TryInitializeStaComCore(InitializeStaCom);

    public void UninitializeCom()
        => PInvoke.CoUninitialize();

    public ShellExecutePropertiesResult ExecutePropertiesVerb(string objectName)
        => ExecutePropertiesVerbCore(ExecutePropertiesVerbNative, objectName);

    private static bool ShowObjectPropertiesCore(
        Func<string, bool> showObjectProperties,
        string objectName)
        => showObjectProperties(objectName);

    private static bool TryInitializeStaComCore(Func<int> initializeStaCom)
        => initializeStaCom() is 0 or 1;

    private static ShellExecutePropertiesResult ExecutePropertiesVerbCore(
        Func<string, ShellExecutePropertiesResult> executePropertiesVerb,
        string objectName)
        => executePropertiesVerb(objectName);

    private static unsafe int InitializeStaCom()
        => PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);

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
}
