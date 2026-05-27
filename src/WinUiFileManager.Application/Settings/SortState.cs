using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Settings;

[StructLayout(LayoutKind.Auto)]
public readonly record struct SortState(SortColumn Column, bool Ascending)
{
    public static SortState Default { get; } = new(SortColumn.Name, Ascending: true);
}
