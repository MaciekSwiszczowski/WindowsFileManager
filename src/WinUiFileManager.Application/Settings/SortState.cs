using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Settings;

/// <summary>
/// Persisted sort state for a pane (which <see cref="SortColumn"/> and direction), part of
/// <see cref="AppSettings"/>. A value struct stored inline.
/// </summary>
/// <param name="Column">The column to sort by.</param>
/// <param name="Ascending"><see langword="true"/> for ascending order; <see langword="false"/> for descending.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SortState(SortColumn Column, bool Ascending)
{
    /// <summary>Default sort: by name, ascending.</summary>
    public static SortState Default { get; } = new(SortColumn.Name, Ascending: true);
}
