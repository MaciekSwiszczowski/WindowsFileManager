namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// The result of a <see cref="FileEntryObservableRowStore.AddOrUpdate"/> call: what changed and where, so a
/// UI-bound collection kept in lockstep with the store can apply a single matching operation rather than a
/// full rebuild. Indices are positions in the store's sorted row list at the moment of the change.
/// </summary>
/// <param name="Kind">The kind of change applied.</param>
/// <param name="Index">The resulting index of the affected row (insert/replace position, or move target).</param>
/// <param name="FromIndex">For <see cref="RowMutationKind.Moved"/>, the row's prior index; otherwise equal
/// to <paramref name="Index"/>.</param>
internal readonly record struct RowMutation(RowMutationKind Kind, int Index, int FromIndex)
{
    /// <summary>A new row inserted at <paramref name="index"/>.</summary>
    public static RowMutation Inserted(int index) => new(RowMutationKind.Inserted, index, index);

    /// <summary>An existing row replaced in place at <paramref name="index"/>.</summary>
    public static RowMutation Replaced(int index) => new(RowMutationKind.Replaced, index, index);

    /// <summary>An existing row removed from <paramref name="fromIndex"/> and re-inserted at
    /// <paramref name="toIndex"/> (the index in the list after the removal).</summary>
    public static RowMutation Moved(int fromIndex, int toIndex) => new(RowMutationKind.Moved, toIndex, fromIndex);
}
