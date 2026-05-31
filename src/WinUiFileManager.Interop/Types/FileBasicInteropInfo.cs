using System.Runtime.InteropServices;

namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Immutable projection of <c>FILE_BASIC_INFO</c>: a file's attribute flags and its four NTFS timestamps.
/// Produced by <see cref="Adapters.IFileSystemMetadataInterop.TryGetFileBasicInfo"/>.
/// </summary>
/// <remarks>
/// Timestamps are raw FILETIME ticks (100-ns intervals since 1601-01-01 UTC), not <see cref="System.DateTime"/>,
/// so the value crosses the interop boundary without timezone/clock interpretation. Laid out
/// <see cref="LayoutKind.Sequential"/> to mirror the native struct.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct FileBasicInteropInfo
{
    /// <summary>Win32 file attribute flags.</summary>
    public uint FileAttributes { get; }

    /// <summary>Creation time as FILETIME ticks.</summary>
    public long CreationTime { get; }

    /// <summary>Last-access time as FILETIME ticks.</summary>
    public long LastAccessTime { get; }

    /// <summary>Last-write (content modification) time as FILETIME ticks.</summary>
    public long LastWriteTime { get; }

    /// <summary>NTFS change time (metadata modification) as FILETIME ticks.</summary>
    public long ChangeTime { get; }

    /// <summary>Creates a basic-info record from native attributes and the four FILETIME timestamps.</summary>
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
