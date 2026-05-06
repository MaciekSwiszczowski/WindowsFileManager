using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.SafeHandles;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class FileSystemMetadataInterop : IFileSystemMetadataInterop
{
    private const uint FileReadAttributesAccess = 0x80;
    private const uint GetFinalPathNameNormalizedFlag = 0;

    public SafeFileHandle OpenForMetadataRead(string path, bool treatAsDirectory)
    {
        var flags = treatAsDirectory
            ? FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
            : 0;
        var handle = PInvoke.CreateFile(
            path,
            FileReadAttributesAccess,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            flags);

        return handle.IsInvalid
            ? throw new InvalidOperationException($"CreateFileW failed: {Marshal.GetLastPInvokeError()}")
            : handle;
    }

    public bool TryGetFileBasicInfo(SafeFileHandle handle, out FileBasicInteropInfo info)
    {
        unsafe
        {
            FILE_BASIC_INFO basicInfo;
            if (!PInvoke.GetFileInformationByHandleEx(
                    new HANDLE(handle.DangerousGetHandle()),
                    FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo,
                    &basicInfo,
                    (uint)sizeof(FILE_BASIC_INFO)))
            {
                info = default;
                return false;
            }

            info = new FileBasicInteropInfo(
                basicInfo.FileAttributes,
                basicInfo.CreationTime,
                basicInfo.LastAccessTime,
                basicInfo.LastWriteTime,
                basicInfo.ChangeTime);
            return true;
        }
    }

    public bool TryGetFileAttributeReparseTag(SafeFileHandle handle, out uint reparseTag)
    {
        unsafe
        {
            FILE_ATTRIBUTE_TAG_INFO tagInfo;
            if (!PInvoke.GetFileInformationByHandleEx(
                    new HANDLE(handle.DangerousGetHandle()),
                    FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo,
                    &tagInfo,
                    (uint)sizeof(FILE_ATTRIBUTE_TAG_INFO)))
            {
                reparseTag = 0;
                return false;
            }

            reparseTag = tagInfo.ReparseTag;
            return true;
        }
    }

    public bool TryGetNtfsFileIdBytes(SafeFileHandle handle, out byte[]? fileId16)
    {
        unsafe
        {
            FILE_ID_INFO fileIdInfo;
            if (!PInvoke.GetFileInformationByHandleEx(
                    new HANDLE(handle.DangerousGetHandle()),
                    FILE_INFO_BY_HANDLE_CLASS.FileIdInfo,
                    &fileIdInfo,
                    (uint)sizeof(FILE_ID_INFO)))
            {
                fileId16 = null;
                return false;
            }

            var identifier = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<Windows.Win32.__byte_16, byte>(ref fileIdInfo.FileId.Identifier),
                16);
            fileId16 = new byte[identifier.Length];
            identifier.CopyTo(fileId16);
            return true;
        }
    }

    public bool TryGetLegacyFileIndex(SafeFileHandle handle, out LegacyFileIndexInfo info, out int win32Error)
    {
        if (!PInvoke.GetFileInformationByHandle(handle, out var native))
        {
            win32Error = Marshal.GetLastPInvokeError();
            info = default;
            return false;
        }

        win32Error = 0;
        info = new LegacyFileIndexInfo(native.nFileIndexLow, native.nFileIndexHigh, native.nNumberOfLinks);
        return true;
    }

    public string? TryGetVolumeSerialHex(string volumeRootPath)
    {
        if (string.IsNullOrWhiteSpace(volumeRootPath))
        {
            return null;
        }

        unsafe
        {
            uint serial = 0;
            uint maximumComponentLength = 0;
            fixed (char* rootPath = volumeRootPath)
            {
                if (!PInvoke.GetVolumeInformation(rootPath, null, 0, &serial, &maximumComponentLength, null, null, 0))
                {
                    return null;
                }
            }

            return serial.ToString("X8");
        }
    }

    public string? TryGetFinalPath(SafeFileHandle handle)
    {
        unsafe
        {
            Span<char> buffer = stackalloc char[1024];
            fixed (char* bufferPointer = buffer)
            {
                var len = PInvoke.GetFinalPathNameByHandle(
                    new HANDLE(handle.DangerousGetHandle()),
                    bufferPointer,
                    (uint)buffer.Length,
                    GetFinalPathNameNormalizedFlag);
                if (len == 0)
                {
                    return null;
                }

                var sliceLength = (int)Math.Min(len, (uint)buffer.Length);
                return GetNullTerminatedString(buffer[..sliceLength]);
            }
        }
    }

    public IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path)
    {
        var streams = new List<string>();
        unsafe
        {
            WIN32_FIND_STREAM_DATA data;
            fixed (char* pathPointer = path)
            {
                var findHandle = PInvoke.FindFirstStream(
                    pathPointer,
                    STREAM_INFO_LEVELS.FindStreamInfoStandard,
                    &data,
                    0);
                using var handle = new SafeFindFilesHandle(findHandle, ownsHandle: true);
                if (handle.IsInvalid)
                {
                    return streams;
                }

                AddStreamDisplayLine(streams, data);
                while (handle.TryReadNextStream(ref data))
                {
                    AddStreamDisplayLine(streams, data);
                }
            }
        }

        return streams;
    }

    private static void AddStreamDisplayLine(ICollection<string> streams, WIN32_FIND_STREAM_DATA data)
    {
        var streamName = GetNullTerminatedString(data.cStreamName, 296);
        if (string.IsNullOrWhiteSpace(streamName))
        {
            return;
        }

        if (string.Equals(streamName, "::$DATA", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        streams.Add($"{streamName.Trim()} ({data.StreamSize} bytes)");
    }

    private static string GetNullTerminatedString(__char_296 buffer, int length)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<__char_296, char>(ref buffer),
            length);
        return GetNullTerminatedString(span);
    }

    private static string GetNullTerminatedString(ReadOnlySpan<char> buffer)
    {
        var terminatorIndex = buffer.IndexOf('\0');
        var value = terminatorIndex >= 0 ? buffer[..terminatorIndex] : buffer;
        return value.ToString();
    }
}
