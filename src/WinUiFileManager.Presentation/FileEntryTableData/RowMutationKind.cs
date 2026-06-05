namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// How a single <see cref="FileEntryObservableRowStore.AddOrUpdate"/> changed the sorted row list, so a
/// bound projection (e.g. the table's UI collection) can mirror the change with one granular operation
/// instead of rebuilding the whole list.
/// </summary>
internal enum RowMutationKind
{
    /// <summary>A new row was inserted at <see cref="RowMutation.Index"/>.</summary>
    Inserted,

    /// <summary>An existing row was replaced in place at <see cref="RowMutation.Index"/> (its sort key did
    /// not change).</summary>
    Replaced,

    /// <summary>An existing row was replaced and re-positioned: removed from
    /// <see cref="RowMutation.FromIndex"/> and inserted at <see cref="RowMutation.Index"/>.</summary>
    Moved,
}
