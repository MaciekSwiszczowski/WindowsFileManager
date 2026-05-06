using System.Runtime.InteropServices;

namespace WinUiFileManager.Interop.Types;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct FileBasicInteropInfo
{
    internal uint FileAttributes { get; }

    internal long CreationTime { get; }

    internal long LastAccessTime { get; }

    internal long LastWriteTime { get; }

    internal long ChangeTime { get; }

    internal FileBasicInteropInfo(
        uint fileAttributes,
        long creationTime,
        long lastAccessTime,
        long lastWriteTime,
        long changeTime)
    {
        FileAttributes = fileAttributes;
        CreationTime = creationTime;
        LastAccessTime = lastAccessTime;
        LastWriteTime = lastWriteTime;
        ChangeTime = changeTime;
    }
}
