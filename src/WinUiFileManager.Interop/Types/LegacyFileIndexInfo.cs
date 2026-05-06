using System.Runtime.InteropServices;

namespace WinUiFileManager.Interop.Types;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct LegacyFileIndexInfo
{
    public uint FileIndexLow { get; }

    public uint FileIndexHigh { get; }

    public uint NumberOfLinks { get; }

    public LegacyFileIndexInfo(uint fileIndexLow, uint fileIndexHigh, uint numberOfLinks)
    {
        FileIndexLow = fileIndexLow;
        FileIndexHigh = fileIndexHigh;
        NumberOfLinks = numberOfLinks;
    }
}
