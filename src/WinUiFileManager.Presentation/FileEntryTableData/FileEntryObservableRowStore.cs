using System.Diagnostics;
using ObservableCollections;
using R3;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.FileEntryTableData;

/// <summary>
/// Keyed, comparer-sorted row store backed by <see cref="ObservableList{T}"/> - the phase-two
/// ObservableCollections/R3 successor to the DynamicData <c>SourceCache</c> + <c>SortAndBind</c> pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Responsibility is deliberately narrow: in-memory row identity (dedup by <see cref="FilePathKey"/>) and
/// sort order, nothing else. Folder scanning, watcher translation, UI binding, and dispatcher marshalling
/// stay in separate types so this remains a plain data structure.
/// </para>
/// <para>
/// <b>Ordering is incremental.</b> Seeding (<see cref="Reset"/>) sorts once and publishes a single batched
/// change. Per-entry <see cref="AddOrUpdate"/>/<see cref="Remove"/> keep the order with a binary-search
/// insert/locate, so a watcher update costs O(log n) comparisons plus one list shift and emits a single
/// granular Add/Replace/Remove - never a whole-list reset. That is what keeps a bound, virtualized table
/// responsive at 100k+ rows; a full re-sort per update would force the UI to rebuild every time. A genuine
/// reorder (the user changing the sort column) is the only operation that re-sorts the whole list, via
/// <see cref="Sort"/>; coalescing bursts of watcher events is the pipeline layer's job, not this store's.
/// </para>
/// <para>
/// <b>Threading: single-writer, not thread-safe.</b> Every mutating member must run on the one owning
/// thread. The current data source owns it from the UI scheduler; a later synchronized-view adapter can move
/// that ownership to a background pump. The store itself performs no marshalling. A debug-only affinity
/// check enforces the single-writer contract; teardown (<see cref="Clear"/>/<see cref="Dispose"/>) is the one
/// reset point, so it may run on a different thread once writing has stopped.
/// </para>
/// </remarks>
internal sealed class FileEntryObservableRowStore : IDisposable
{
    private readonly Dictionary<FilePathKey, SpecFileEntryViewModel> _rowsByKey = [];

    // The order the list is currently maintained in. Established by Reset/Sort; AddOrUpdate/Remove rely on
    // the list already being ordered by it to binary-search insert/locate positions.
    private IComparer<SpecFileEntryViewModel>? _comparer;
    private bool _disposed;

    // Single-writer affinity state for AssertSingleWriter/ResetWriterAffinity. Both are
    // [Conditional("DEBUG")], so every access to this field is stripped from Release call sites; the field
    // itself remains (one int per store) but is never touched in Release.
    private int _ownerThreadId;

    /// <summary>
    /// The live, sorted row list. Mutated only on the owning writer thread.
    /// </summary>
    public ObservableList<SpecFileEntryViewModel> Rows { get; } = [];

    /// <summary>Returns an R3 stream of row-list changes for downstream table/binding infrastructure.</summary>
    public Observable<CollectionChangedEvent<SpecFileEntryViewModel>> ObserveRowsChanged() => Rows.ObserveChanged();

    /// <summary>
    /// Clears the store and seeds it with a de-duplicated, sorted row set, establishing
    /// <paramref name="comparer"/> as the order maintained by subsequent <see cref="AddOrUpdate"/> calls.
    /// </summary>
    /// <param name="rows">The rows to seed; duplicates by <see cref="FilePathKey"/> resolve last-wins.</param>
    /// <param name="comparer">The order to maintain. Becomes the store's active comparer.</param>
    public void Reset(IEnumerable<SpecFileEntryViewModel> rows, IComparer<SpecFileEntryViewModel> comparer)
    {
        AssertSingleWriter();

        _comparer = comparer;
        _rowsByKey.Clear();
        foreach (var row in rows)
        {
            // Last-wins dedup; GetKey is computed once per row here (it allocates via Path.GetFullPath).
            _rowsByKey[row.GetKey()] = row;
        }

        var sorted = new SpecFileEntryViewModel[_rowsByKey.Count];
        _rowsByKey.Values.CopyTo(sorted, 0);
        Array.Sort(sorted, comparer);

        // One Clear + one batched AddRange instead of Clear + N Adds + a re-sort reset.
        Rows.Clear();
        if (sorted.Length > 0)
        {
            Rows.AddRange(sorted);
        }
    }

    /// <summary>
    /// Inserts a new row in sorted position, or replaces the existing row with the same key and moves it
    /// only if its sort key changed. Maintains the order established by the last <see cref="Reset"/>/
    /// <see cref="Sort"/>; emits a single granular change.
    /// </summary>
    /// <returns>What changed and where, so a bound collection kept in lockstep with <see cref="Rows"/> can
    /// apply one matching operation.</returns>
    /// <exception cref="InvalidOperationException">No sort order has been established yet (call
    /// <see cref="Reset"/> or <see cref="Sort"/> first).</exception>
    public RowMutation AddOrUpdate(SpecFileEntryViewModel row)
    {
        AssertSingleWriter();
        var comparer = _comparer
            ?? throw new InvalidOperationException("AddOrUpdate requires an established sort order; call Reset or Sort first.");

        var key = row.GetKey();
        if (_rowsByKey.TryGetValue(key, out var existingRow))
        {
            _rowsByKey[key] = row;
            return ReplaceSorted(existingRow, row, comparer);
        }

        _rowsByKey.Add(key, row);
        var index = LowerBound(row, comparer);
        Rows.Insert(index, row);
        return RowMutation.Inserted(index);
    }

    /// <summary>Removes the row with the given key.</summary>
    /// <returns>The row's former index in <see cref="Rows"/>, or -1 if no row had that key (so a bound
    /// collection can mirror the removal by index).</returns>
    public int Remove(FilePathKey key)
    {
        AssertSingleWriter();
        if (!_rowsByKey.Remove(key, out var row))
        {
            return -1;
        }

        var index = _comparer is { } comparer ? IndexOfSorted(row, comparer) : Rows.IndexOf(row);
        if (index < 0)
        {
            // Key map and list diverged; should not happen under the single-writer contract.
            Debug.Fail("Row removed from key map was absent from the row list.");
            return -1;
        }

        Rows.RemoveAt(index);
        return index;
    }

    /// <summary>
    /// Re-sorts the whole list under a new comparer and adopts it as the active order. This is the
    /// user-initiated sort-column change; it emits a sort/reset, so it is intentionally distinct from the
    /// incremental <see cref="AddOrUpdate"/> path.
    /// </summary>
    public void Sort(IComparer<SpecFileEntryViewModel> comparer)
    {
        AssertSingleWriter();

        _comparer = comparer;
        Rows.Sort(comparer);
    }

    /// <summary>Clears all stored rows. A reset point for the single-writer affinity contract.</summary>
    public void Clear()
    {
        // Teardown/refresh may legitimately happen on a different thread once writing has stopped; reset the
        // single-writer affinity (rather than asserting it) so the next mutation re-establishes ownership.
        ResetWriterAffinity();
        _rowsByKey.Clear();
        Rows.Clear();
    }

    /// <summary>Clears the store. Kept for ownership symmetry with future bindable view adapters.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Clear();
    }

    /// <summary>Replaces <paramref name="existingRow"/> with <paramref name="newRow"/>, keeping sort order.
    /// Stays in place with a single Replace when the changed row still sorts between its neighbours
    /// (the common case where the edited field is not the active sort key); otherwise moves it.</summary>
    /// <returns>The granular change applied, for mirroring to a bound collection.</returns>
    private RowMutation ReplaceSorted(
        SpecFileEntryViewModel existingRow,
        SpecFileEntryViewModel newRow,
        IComparer<SpecFileEntryViewModel> comparer)
    {
        var oldIndex = IndexOfSorted(existingRow, comparer);
        if (oldIndex < 0)
        {
            Debug.Fail("Existing row tracked by key was absent from the row list.");
            var insertIndex = LowerBound(newRow, comparer);
            Rows.Insert(insertIndex, newRow);
            return RowMutation.Inserted(insertIndex);
        }

        if (StaysInPlace(oldIndex, newRow, comparer))
        {
            Rows[oldIndex] = newRow;
            return RowMutation.Replaced(oldIndex);
        }

        Rows.RemoveAt(oldIndex);
        var newIndex = LowerBound(newRow, comparer);
        Rows.Insert(newIndex, newRow);
        return RowMutation.Moved(oldIndex, newIndex);
    }

    /// <summary>True when a row substituted at <paramref name="index"/> would still be correctly ordered
    /// relative to its immediate neighbours, so no move is needed.</summary>
    private bool StaysInPlace(int index, SpecFileEntryViewModel row, IComparer<SpecFileEntryViewModel> comparer)
    {
        if (index > 0 && comparer.Compare(Rows[index - 1], row) > 0)
        {
            return false;
        }

        return index >= Rows.Count - 1 || comparer.Compare(row, Rows[index + 1]) <= 0;
    }

    /// <summary>First index at which <paramref name="row"/> can be inserted to keep the list ordered by
    /// <paramref name="comparer"/> (the lower bound of its sort position).</summary>
    private int LowerBound(SpecFileEntryViewModel row, IComparer<SpecFileEntryViewModel> comparer)
    {
        var lo = 0;
        var hi = Rows.Count;
        while (lo < hi)
        {
            var mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (comparer.Compare(Rows[mid], row) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>Locates the exact row instance in the sorted list by binary-searching to its sort band and
    /// matching by reference (within one folder, equal comparison implies the same key, hence the same
    /// instance). Falls back to a linear scan if the list is not ordered by this comparer.</summary>
    private int IndexOfSorted(SpecFileEntryViewModel row, IComparer<SpecFileEntryViewModel> comparer)
    {
        for (var i = LowerBound(row, comparer); i < Rows.Count && comparer.Compare(Rows[i], row) == 0; i++)
        {
            if (ReferenceEquals(Rows[i], row))
            {
                return i;
            }
        }

        return Rows.IndexOf(row);
    }

    /// <summary>Debug-only enforcement of the single-writer contract: captures the first mutating thread
    /// and asserts every later mutation runs on it. All call sites are stripped from Release builds.</summary>
    [Conditional("DEBUG")]
    private void AssertSingleWriter()
    {
        var current = Environment.CurrentManagedThreadId;
        if (_ownerThreadId == 0)
        {
            _ownerThreadId = current;
            return;
        }

        Debug.Assert(
            _ownerThreadId == current,
            "FileEntryObservableRowStore is single-writer: all mutations must occur on the owning writer thread.");
    }

    /// <summary>Clears the captured writer thread so the next mutation re-establishes affinity, letting a
    /// teardown/refresh on a different thread proceed without tripping <see cref="AssertSingleWriter"/>.
    /// Call sites are stripped from Release builds along with <see cref="AssertSingleWriter"/>.</summary>
    [Conditional("DEBUG")]
    private void ResetWriterAffinity() => _ownerThreadId = 0;
}
