using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using WinUiFileManager.Interop.SafeHandles;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// CsWin32-backed adapter that reads low-level NTFS metadata for a file or directory: basic timestamps and
/// attributes, reparse tag, NTFS 128-bit and legacy 64-bit file identity, volume serial, the canonical final
/// path, and the list of alternate data streams. Implements both <see cref="IFileSystemMetadataInterop"/> and
/// <see cref="IAlternateDataStreamInterop"/> because both contracts are served by the same Win32 handle-based
/// queries. This is the single point where the relevant <c>Windows.Win32.*</c> bindings are used.
/// </summary>
/// <remarks>
/// Ownership/threading: <see cref="OpenForMetadataRead"/> returns a <see cref="SafeFileHandle"/> the caller owns
/// and must dispose. The <c>TryGet*</c> methods read from a caller-supplied handle and never take ownership of
/// it. Most methods use <c>unsafe</c> blocks to pass fixed buffers/structs to the native calls; the pointers are
/// only valid for the duration of each call. The <c>Try*</c> pattern reports failure via <see langword="bool"/>
/// (sometimes with an out Win32 error) rather than throwing, except <see cref="OpenForMetadataRead"/> which
/// throws on a failed open.
/// </remarks>
internal sealed class FileSystemMetadataInterop : IFileSystemMetadataInterop, IAlternateDataStreamInterop
{
    // FILE_READ_ATTRIBUTES (0x80): the minimal access right needed for the metadata queries below. Opening with
    // only this right avoids requiring read/share rights on locked or protected files.
    private const uint FileReadAttributesAccess = 0x80;
    // FILE_NAME_NORMALIZED (0): request the normalized final path form from GetFinalPathNameByHandle.
    private const uint GetFinalPathNameNormalizedFlag = 0;

    /// <summary>
    /// Opens a metadata-only handle to <paramref name="path"/> with full sharing so it never blocks other openers.
    /// </summary>
    /// <param name="path">Path to open (extended-length form supported).</param>
    /// <param name="treatAsDirectory">
    /// When <see langword="true"/>, passes <c>FILE_FLAG_BACKUP_SEMANTICS</c> so a directory handle can be obtained
    /// (required by Win32 to open a directory with <c>CreateFileW</c>).
    /// </param>
    /// <returns>An owned <see cref="SafeFileHandle"/>; the caller is responsible for disposing it.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <c>CreateFileW</c> fails; the message carries the Win32 error.</exception>
    public SafeFileHandle OpenForMetadataRead(string path, bool treatAsDirectory)
    {
        var flags = treatAsDirectory
            ? FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS
            : 0;
        // FILE_SHARE_READ|WRITE|DELETE: never lock the target — this is a read-only metadata probe.
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

    /// <summary>Reads <c>FILE_BASIC_INFO</c> (attributes and the four NTFS timestamps) for the open handle.</summary>
    /// <param name="handle">A handle opened for metadata read; not disposed by this method.</param>
    /// <param name="info">Receives the basic info on success; <c>default</c> on failure.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the native query failed.</returns>
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

    /// <summary>Reads the file's reparse tag via <c>FILE_ATTRIBUTE_TAG_INFO</c> (identifies symlinks, mount points, cloud placeholders, etc.).</summary>
    /// <param name="handle">A handle opened for metadata read; not disposed by this method.</param>
    /// <param name="reparseTag">Receives the reparse tag on success; <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the native query failed.</returns>
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

    /// <summary>Reads the NTFS 128-bit file identity (<c>FILE_ID_INFO</c>), the modern stable file ID.</summary>
    /// <param name="handle">A handle opened for metadata read; not disposed by this method.</param>
    /// <param name="fileId16">Receives a freshly-allocated 16-byte identifier on success; <see langword="null"/> on failure.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the native query failed.</returns>
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

            // FileId.Identifier is a fixed 16-byte buffer; reinterpret it as a byte span and copy out so the
            // result outlives this stack frame (the native struct does not).
            var identifier = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<__byte_16, byte>(ref fileIdInfo.FileId.Identifier),
                16);
            fileId16 = new byte[identifier.Length];
            identifier.CopyTo(fileId16);
            return true;
        }
    }

    /// <summary>
    /// Reads the legacy 64-bit file index and hard-link count via <c>GetFileInformationByHandle</c>
    /// (<c>BY_HANDLE_FILE_INFORMATION</c>). Complements <see cref="TryGetNtfsFileIdBytes"/> for callers/filesystems
    /// that only expose the older identity form.
    /// </summary>
    /// <param name="handle">A handle opened for metadata read; not disposed by this method.</param>
    /// <param name="info">Receives the index (low/high) and link count on success; <c>default</c> on failure.</param>
    /// <param name="win32Error">The captured Win32 error on failure; <c>0</c> on success.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the native query failed.</returns>
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

    /// <summary>
    /// Reads the volume serial number for <paramref name="volumeRootPath"/> and formats it as 8 uppercase hex
    /// digits (the real serial, unlike <see cref="VolumeInterop"/>'s placeholder).
    /// </summary>
    /// <param name="volumeRootPath">The volume root (e.g. <c>C:\</c>).</param>
    /// <returns>The serial as <c>X8</c> hex, or <see langword="null"/> when the path is blank or the query fails.</returns>
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
            // Only the serial and max-component-length out params are requested; label/filesystem-name buffers
            // are passed as null because callers of this method only need the serial.
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

    /// <summary>
    /// Resolves the canonical final path of the open handle via <c>GetFinalPathNameByHandle</c>, following
    /// symlinks/junctions to the real target. Used to detect links and reveal where a path actually points.
    /// </summary>
    /// <param name="handle">A handle opened for metadata read; not disposed by this method.</param>
    /// <returns>The normalized final path (may carry the <c>\\?\</c> prefix), or <see langword="null"/> on failure.</returns>
    public string? TryGetFinalPath(SafeFileHandle handle)
    {
        unsafe
        {
            // 1024 chars covers extended-length paths without a heap allocation. If the true path were longer the
            // returned length would exceed the buffer; the Min() below clamps to what was actually written.
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

    /// <summary>
    /// Enumerates the file's NTFS alternate data streams via <c>FindFirstStreamW</c>/<c>FindNextStreamW</c>,
    /// returning a display line ("name (N bytes)") per stream. The unnamed primary data stream is excluded.
    /// </summary>
    /// <param name="path">Path to the file to inspect.</param>
    /// <returns>Display lines for each ADS; empty (never <see langword="null"/>) when there are none or the open fails.</returns>
    public IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path)
    {
        // Allocated lazily on the first *alternate* stream. Files without ADS (the overwhelmingly common case)
        // therefore allocate no backing list and return a shared empty instance, keeping this path zero-GC.
        List<string>? streams = null;
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
                // SafeFindFilesHandle owns the search handle and guarantees FindClose; the `using` releases it
                // even if AddStreamDisplayLine throws. FindFirstStreamW already returned the first stream in
                // `data`, so it is processed before the loop pulls subsequent streams.
                using var handle = new SafeFindFilesHandle(findHandle);
                if (handle.IsInvalid)
                {
                    return [];
                }

                AddStreamDisplayLine(ref streams, data);
                while (handle.TryReadNextStream(ref data))
                {
                    AddStreamDisplayLine(ref streams, data);
                }
            }
        }

        return streams ?? (IReadOnlyList<string>)[];
    }

    private static void AddStreamDisplayLine(ref List<string>? streams, WIN32_FIND_STREAM_DATA data)
    {
        // cStreamName is a fixed 296-char buffer; read up to its null terminator.
        var streamName = GetNullTerminatedString(data.cStreamName, 296);
        if (string.IsNullOrWhiteSpace(streamName))
        {
            return;
        }

        // "::$DATA" is the unnamed primary data stream (the file's normal contents) — not an *alternate* stream.
        if (string.Equals(streamName, "::$DATA", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        streams ??= [];
        streams.Add($"{streamName.Trim()} ({data.StreamSize} bytes)");
    }

    // Reinterprets a fixed CsWin32 char buffer (__char_296) as a span so the shared terminator-trimming helper
    // can be reused. `buffer` is taken by value (a copy of the fixed array) which is acceptable for a one-shot read.
    private static string GetNullTerminatedString(__char_296 buffer, int length)
    {
        var span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<__char_296, char>(ref buffer), length);
        return GetNullTerminatedString(span);
    }

    // Win32 fills fixed-size buffers and null-terminates; trim at the first '\0' rather than returning trailing garbage.
    private static string GetNullTerminatedString(ReadOnlySpan<char> buffer)
    {
        var terminatorIndex = buffer.IndexOf('\0');
        var value = terminatorIndex >= 0 ? buffer[..terminatorIndex] : buffer;
        return value.ToString();
    }
}
