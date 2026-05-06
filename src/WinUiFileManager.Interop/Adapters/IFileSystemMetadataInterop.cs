using Microsoft.Win32.SafeHandles;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

internal interface IFileSystemMetadataInterop
{
    public SafeFileHandle OpenForMetadataRead(string path, bool treatAsDirectory);

    public bool TryGetFileBasicInfo(SafeFileHandle handle, out FileBasicInteropInfo info);

    public bool TryGetFileAttributeReparseTag(SafeFileHandle handle, out uint reparseTag);

    public bool TryGetNtfsFileIdBytes(SafeFileHandle handle, out byte[]? fileId16);

    public bool TryGetLegacyFileIndex(SafeFileHandle handle, out LegacyFileIndexInfo info, out int win32Error);

    public string? TryGetVolumeSerialHex(string volumeRootPath);

    public string? TryGetFinalPath(SafeFileHandle handle);

    public IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path);
}
