using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed adapter over the Win32 Shell APIs used to surface the native file/folder
/// "Properties" dialog and to manage the apartment-threaded COM apartment those shell calls require.
/// This is the Interop-layer implementation of <see cref="IShellInterop"/>; Infrastructure consumes it
/// only through that abstraction (per the layering rule that Infrastructure never touches
/// <c>Windows.Win32.*</c> directly).
/// </summary>
/// <remarks>
/// Threading: the shell "Properties" verb and COM property handlers must run on an STA thread, so callers
/// initialize the apartment via <see cref="TryInitializeStaCom"/> before invoking these members. Several
/// members are static helper "Core" methods that take the native call as a delegate purely to make the
/// thin marshalling layer unit-testable without P/Invoking the real shell.
/// </remarks>
internal sealed class ShellInterop : IShellInterop
{
    // SEE_MASK_INVOKEIDLIST: tells ShellExecuteEx to use the IContextMenu IDList path so the
    // "properties" verb is resolved exactly as Explorer would resolve it.
    private const uint SeeMaskInvokeIdList = 0x0000000C;

    /// <summary>
    /// Opens the native Shell property sheet for <paramref name="objectName"/> via <c>SHObjectProperties</c>.
    /// </summary>
    /// <param name="objectName">Fully-qualified file-system path of the object whose properties to show.</param>
    /// <param name="lastError">On failure, the captured Win32 error code; <c>0</c> when the call succeeds.</param>
    /// <returns><see langword="true"/> if the property sheet was shown; otherwise <see langword="false"/>.</returns>
    public bool ShowObjectProperties(string objectName, out int lastError)
    {
        var result = ShowObjectPropertiesCore(static path => PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path), objectName);
        lastError = result ? 0 : Marshal.GetLastWin32Error();
        return result;
    }

    /// <summary>
    /// Initializes COM for the calling thread as single-threaded apartment (STA).
    /// </summary>
    /// <returns><see langword="true"/> when the apartment is usable for shell calls.</returns>
    /// <remarks>
    /// KNOWN HAZARD (do not silently "fix" without auditing the matching <see cref="UninitializeCom"/> call):
    /// <c>CoInitializeEx</c> returns <c>S_OK</c> (0) when *we* initialized COM and <c>S_FALSE</c> (1) when COM
    /// was *already* initialized on this thread. Per the COM rules, <see cref="UninitializeCom"/> /
    /// <c>CoUninitialize</c> must only be called when we received <c>S_OK</c>. This method currently treats
    /// both 0 and 1 as "success" (see <see cref="TryInitializeStaComCore"/>) and does not propagate which of
    /// the two occurred, so an unconditional <see cref="UninitializeCom"/> after an <c>S_FALSE</c> result is an
    /// over-release of the apartment.
    /// </remarks>
    public bool TryInitializeStaCom()
        => TryInitializeStaComCore(InitializeStaCom);

    /// <summary>
    /// Calls <c>CoUninitialize</c> for the current thread.
    /// </summary>
    /// <remarks>
    /// Must be balanced against a <see cref="TryInitializeStaCom"/> that returned <c>S_OK</c> only. See the
    /// hazard note on <see cref="TryInitializeStaCom"/>: this unconditional call can over-release the apartment
    /// when COM was already initialized (<c>S_FALSE</c>).
    /// </remarks>
    public void UninitializeCom()
        => PInvoke.CoUninitialize();

    /// <summary>
    /// Invokes the shell "properties" verb on <paramref name="objectName"/> via <c>ShellExecuteEx</c>.
    /// </summary>
    /// <param name="objectName">Fully-qualified file-system path of the target object.</param>
    /// <returns>
    /// A <see cref="ShellExecutePropertiesResult"/> carrying success, the captured Win32 error on failure, and
    /// the raw <c>hInstApp</c> value returned by the shell.
    /// </returns>
    /// <remarks>Must run on an STA thread initialized via <see cref="TryInitializeStaCom"/>.</remarks>
    public ShellExecutePropertiesResult ExecutePropertiesVerb(string objectName)
        => ExecutePropertiesVerbCore(ExecutePropertiesVerbNative, objectName);

    // Seam for tests: lets the marshalling logic be exercised against a fake without touching the real shell.
    private static bool ShowObjectPropertiesCore(
        Func<string, bool> showObjectProperties,
        string objectName)
        => showObjectProperties(objectName);

    // 0 == S_OK (we initialized), 1 == S_FALSE (already initialized). Both are treated as "apartment usable",
    // which is fine for *entering* the apartment but loses the information needed to decide whether the matching
    // CoUninitialize is safe. See the hazard note on TryInitializeStaCom.
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
        // The verb and path strings must stay pinned for the duration of the ShellExecuteEx call because the
        // SHELLEXECUTEINFOW struct holds raw pointers into them.
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
                nShow = 5 // SW_SHOW
            };

            var success = PInvoke.ShellExecuteEx(ref executeInfo);
            return new ShellExecutePropertiesResult(
                success,
                success ? 0 : Marshal.GetLastWin32Error(),
                (nint)executeInfo.hInstApp.Value);
        }
    }
}
