using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WinUiFileManager.Interop.SafeHandles;

internal sealed class SafeFindFilesHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal unsafe SafeFindFilesHandle(HANDLE preexistingHandle, bool ownsHandle)
        : base(ownsHandle)
    {
        SetHandle((nint)preexistingHandle.Value);
    }

    internal HANDLE DangerousGetHandleForPInvoke() => new(DangerousGetHandle());

    protected override bool ReleaseHandle()
    {
        return PInvoke.FindClose(new HANDLE(handle));
    }
}
