using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WinUiFileManager.Interop.SafeHandles;

internal sealed class SafeFindFilesHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly Func<IntPtr, bool> _releaseHandle;

    public SafeFindFilesHandle()
        : this(IntPtr.Zero, ownsHandle: true)
    {
    }

    internal SafeFindFilesHandle(IntPtr preexistingHandle, bool ownsHandle, Func<IntPtr, bool>? releaseHandle = null)
        : base(ownsHandle)
    {
        SetHandle(preexistingHandle);
        _releaseHandle = releaseHandle ?? FindCloseCore;
    }

    protected override bool ReleaseHandle()
    {
        return _releaseHandle(handle);
    }

    private static bool FindCloseCore(IntPtr handle)
    {
        return PInvoke.FindClose(new HANDLE(handle));
    }
}
