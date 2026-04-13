using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public sealed class FileIdentityInterop : IFileIdentityInterop
{
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
}
