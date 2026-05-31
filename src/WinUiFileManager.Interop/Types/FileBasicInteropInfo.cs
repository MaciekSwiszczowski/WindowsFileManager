using System.Runtime.InteropServices;

namespace WinUiFileManager.Interop.Types;

[StructLayout(LayoutKind.Sequential)]
public readonly struct FileBasicInteropInfo
{
    public uint FileAttributes { get; }

    public long CreationTime { get; }

    public long LastAccessTime { get; }

    public long LastWriteTime { get; }

    public long ChangeTime { get; }

    public FileBasicInteropInfo(
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
