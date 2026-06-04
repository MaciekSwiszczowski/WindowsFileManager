using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed adapter for low-level delete operations. It deliberately exposes single-entry operations only;
/// traversal and force-delete policy belong to callers in higher layers.
/// </summary>
internal sealed class FileDeletionInterop : IFileDeletionInterop
{
    public InteropResult SetAttributes(string path, FileAttributes attributes)
    {
        return PInvoke.SetFileAttributes(path, (FILE_FLAGS_AND_ATTRIBUTES)(uint)attributes)
            ? InteropResult.Ok()
            : CreateFailure(nameof(PInvoke.SetFileAttributes), path);
    }

    public InteropResult DeleteFile(string path)
    {
        return PInvoke.DeleteFile(path)
            ? InteropResult.Ok()
            : CreateFailure(nameof(PInvoke.DeleteFile), path);
    }

    public InteropResult RemoveDirectory(string path)
    {
        return PInvoke.RemoveDirectory(path)
            ? InteropResult.Ok()
            : CreateFailure(nameof(PInvoke.RemoveDirectory), path);
    }

    private static InteropResult CreateFailure(string operation, string path)
    {
        var errorCode = Marshal.GetLastPInvokeError();
        return InteropResult.Fail(errorCode, $"{operation} failed for '{path}' with Win32 error {errorCode}.");
    }
}
