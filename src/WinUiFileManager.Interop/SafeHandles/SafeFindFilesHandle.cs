using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace WinUiFileManager.Interop.SafeHandles;

internal sealed class SafeFindFilesHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal unsafe SafeFindFilesHandle(HANDLE preexistingHandle, bool ownsHandle)
        : base(ownsHandle)
        => SetHandle((nint)preexistingHandle.Value);

    private HANDLE DangerousGetHandleForPInvoke() => new(DangerousGetHandle());

    internal unsafe bool TryReadNextStream(ref WIN32_FIND_STREAM_DATA data)
    {
        fixed (WIN32_FIND_STREAM_DATA* p = &data)
        {
            return PInvoke.FindNextStream(DangerousGetHandleForPInvoke(), p);
        }
    }

    protected override bool ReleaseHandle() => PInvoke.FindClose(new HANDLE(handle));
}
