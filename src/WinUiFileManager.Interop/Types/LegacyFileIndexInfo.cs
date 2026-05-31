using System.Runtime.InteropServices;

namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Immutable projection of the legacy NTFS file identity from <c>BY_HANDLE_FILE_INFORMATION</c>: the 64-bit file
/// index (split into low/high words) plus the hard-link count. Produced by
/// <see cref="Adapters.IFileSystemMetadataInterop.TryGetLegacyFileIndex"/>.
/// </summary>
/// <remarks>
/// This is the pre-Windows-8 identity form; prefer the 128-bit
/// <see cref="Adapters.IFileSystemMetadataInterop.TryGetNtfsFileIdBytes"/> where available. Laid out
/// <see cref="LayoutKind.Sequential"/> to mirror the native field order.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct LegacyFileIndexInfo
{
    /// <summary>Low 32 bits of the file index (<c>nFileIndexLow</c>).</summary>
    public uint FileIndexLow { get; }

    /// <summary>High 32 bits of the file index (<c>nFileIndexHigh</c>).</summary>
    public uint FileIndexHigh { get; }

    /// <summary>Number of hard links to the file (<c>nNumberOfLinks</c>).</summary>
    public uint NumberOfLinks { get; }

    /// <summary>Creates a legacy file-index record from the native low/high index words and link count.</summary>
    public LegacyFileIndexInfo(uint fileIndexLow, uint fileIndexHigh, uint numberOfLinks)
    {
        FileIndexLow = fileIndexLow;
        FileIndexHigh = fileIndexHigh;
        NumberOfLinks = numberOfLinks;
    }
}
