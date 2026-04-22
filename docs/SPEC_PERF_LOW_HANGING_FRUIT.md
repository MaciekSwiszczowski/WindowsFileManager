# Spec: Performance & Memory Low-Hanging Fruit

Scope: a small, prescriptive set of micro-optimizations in the existing code that (a) are each ≤ 50 LOC of diff, (b) target a named callsite with measurable allocation or CPU cost on a 100 000-file folder, and (c) are not covered by any of `SPEC_BUG_FIXES.md`, `SPEC_NUGET_MODERNIZATION.md`, `SPEC_NATIVE_MODERNIZATION.md`, or `SPEC_RENAME_BUGS.md`.

This spec is **not** a general-purpose performance sweep. Items that would need benchmarks to justify (thumbnail pipeline, watcher throttling tuning, TableView virtualization hardening) are explicitly out of scope — they belong in their own specs after this batch lands.

Landing order: **after `SPEC_NATIVE_MODERNIZATION.md` M-5**, before the keyboard-shortcut spec.

## 1. Goals

1. Stop recomputing deterministic strings on every property read in hot paths (`NormalizedPath.DisplayPath`, `FileEntryViewModel.LastWriteTime` / `CreationTime`).
2. Stop allocating the same hash-set twice during watcher batches.
3. Leave every other perf concern to the already-tracked specs.

## 2. Findings

### P-1. Memoize `NormalizedPath.DisplayPath`

**File:** [`src/WinUiFileManager.Domain/ValueObjects/NormalizedPath.cs`](../src/WinUiFileManager.Domain/ValueObjects/NormalizedPath.cs)

**Today.** `DisplayPath` is a computed property on the `readonly record struct`:

```csharp
public string DisplayPath =>
    Value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
        ? Value[ExtendedPathPrefix.Length..]
        : Value;
```

The substring `Value[ExtendedPathPrefix.Length..]` allocates a fresh `string` every access. `DisplayPath` is read from at least three hot paths:

- [`FileEntryViewModel.UniqueKey`](../src/WinUiFileManager.Presentation/ViewModels/FileEntryViewModel.cs) — `UniqueKey => Model.FullPath.DisplayPath`. Called by `DynamicData.SourceCache` on every `AddOrUpdate` and by every `Lookup` that the VM performs (rename flow, watcher apply, selection queries).
- [`FileEntryViewModel.FullPath`](../src/WinUiFileManager.Presentation/ViewModels/FileEntryViewModel.cs) — read by inspector category loaders, "copy full path" command, tooltips.
- `ErrorMessage` / log lines that format the path.

On a 100 000-file folder a single navigation triggers ~100 000 `AddOrUpdate` calls, each with at least one `UniqueKey` read and therefore one `DisplayPath` allocation — roughly 200 KB of short-lived strings that did not need to exist.

**Fix.** Compute `DisplayPath` once, at construction, and store it alongside `Value`:

```csharp
public readonly record struct NormalizedPath
{
    private const string ExtendedPathPrefix = @"\\?\";

    public NormalizedPath(string value)
    {
        Value = value;
        DisplayPath = value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? value[ExtendedPathPrefix.Length..]
            : value;
    }

    public string Value { get; init; }
    public string DisplayPath { get; init; }

    public static NormalizedPath FromUserInput(string path) { /* unchanged */ }
    public static implicit operator NormalizedPath(string path) => FromUserInput(path);
    public override string ToString() => Value;
}
```

Cost: one additional string reference per struct (8 bytes on x64). Saved: one allocation per `DisplayPath` access, which is the overwhelming majority of reads. Record `Equals` still uses `Value` (the canonical form), so no equality change.

**Tests.** Update `NormalizedPathTests` (create the file if absent):
- `Test_DisplayPath_StripsExtendedPrefix`
- `Test_DisplayPath_LeavesShortPathUntouched`
- `Test_DisplayPath_IsStableAcrossMultipleReads` — same reference for repeated reads.
- `Test_RecordEquality_IgnoresDisplayPathCache` — two `NormalizedPath`s with equal `Value` are equal.

**Size / risk:** ~15 LOC. Low — `DisplayPath` is a read-only projection; nothing writes to it. The `{ get; init; }` on both fields keeps the struct `readonly`-compatible.

### P-2. Collapse the double `ToHashSet()` in `ApplyWatcherBatch`

**File:** [`src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs`](../src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs) ≈ lines 718–750.

**Today.** On every watcher-batch flush, `ApplyWatcherBatch` builds a `HashSet<string>` of selected paths, uses it for the initial reconciliation, then rebuilds another `HashSet<string>` after running rename reconciliation via `.Select(...).ToHashSet()`. Both sets contain the same data except for the renamed paths. In a pane with 10 000 selected items (unlikely but legal), this is two 10 000-entry hash-set allocations per watcher tick.

**Fix.** Allocate the set once, then mutate in place when renames are applied:

```csharp
var selectedPaths = currentlySelectedKeys.ToHashSet(StringComparer.Ordinal);

if (batch.RenamedPaths.Count > 0)
{
    foreach (var rename in batch.RenamedPaths)
    {
        if (selectedPaths.Remove(rename.OldPath.DisplayPath))
        {
            selectedPaths.Add(rename.NewPath.DisplayPath);
        }
    }
}

// Use selectedPaths for the remainder of the batch reconciliation.
```

Net effect: one `HashSet<string>` allocation per batch instead of two.

**Tests.** Add to `FilePaneViewModelWatcherTests`:
- `Test_ApplyWatcherBatch_KeepsRenamedItemSelected` — precondition: select `a.txt`, fire watcher batch that renames `a.txt → b.txt`, assert `b.txt` is selected and `a.txt` is not.
- (Regression gate — the behavior already works; the test locks it in so the refactor can't regress.)

**Size / risk:** ~20 LOC including test. Low — single method, isolated refactor. Keep the existing fallback for the non-rename common case (no allocation changes for folders without watcher activity).

### P-3. Cache formatted time strings on `FileEntryViewModel`

**File:** [`src/WinUiFileManager.Presentation/ViewModels/FileEntryViewModel.cs`](../src/WinUiFileManager.Presentation/ViewModels/FileEntryViewModel.cs) ≈ lines 43-45.

**Today.** The properties:

```csharp
public string LastWriteTime => IsParentEntry ? string.Empty : Model.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
public string CreationTime => IsParentEntry ? string.Empty : Model.CreationTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
```

Each call performs a `ToLocalTime()` and a culture-aware `DateTime.ToString(format)`, both of which allocate. `WinUI.TableView` reads the bound properties during row realization; a fast scroll down a 100 000-file folder realizes ~50 rows per layout pass, so the cost per scroll is ~100 allocations (50 rows × 2 columns). Minor per frame, but the properties are also read from the inspector, from tooltips, and from any future column reorder — every read is a fresh allocation.

`FileEntryViewModel` is immutable with respect to `Model`; the formatted strings are deterministic. Cache them.

**Fix.** Compute once, in the constructor, and expose the cached strings:

```csharp
public sealed partial class FileEntryViewModel : ObservableObject
{
    public FileEntryViewModel(FileSystemEntryModel model)
    {
        Model = model;
        LastWriteTime = FormatTimestamp(model.LastWriteTimeUtc);
        CreationTime = FormatTimestamp(model.CreationTimeUtc);
    }

    private FileEntryViewModel()
    {
        Model = null!;
        IsParentEntry = true;
        LastWriteTime = string.Empty;
        CreationTime = string.Empty;
    }

    public string LastWriteTime { get; }
    public string CreationTime { get; }

    private static string FormatTimestamp(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
```

Add `CultureInfo.InvariantCulture` to the format call — the existing code uses the current culture, which in production happens to format identically for the given pattern but is a latent bug: in a locale with a non-Gregorian calendar (Thai Buddhist, etc.), the year shifts. Use invariant for stability.

Cost: two additional string references per `FileEntryViewModel` (16 bytes on x64). With 100 000 entries that's 1.6 MB retained — acceptable given these entries already hold a `FileSystemEntryModel` with its own path string (≥ 40 bytes typical).

**Tests.** Update `FileEntryViewModelTests` (create the file if absent):
- `Test_LastWriteTime_FormattedOnce_StableReference` — same reference across multiple reads.
- `Test_LastWriteTime_UsesInvariantCulture` — set `CultureInfo.CurrentCulture` to `th-TH`, verify the year is Gregorian.
- Covering `CreationTime` as well.

**Size / risk:** ~30 LOC. Low — the properties become plain `{ get; }` auto-properties, so every existing binding continues to work. The parent-entry sentinel branch is handled in the private constructor.

## 3. Out of scope (explicit non-goals for this spec)

These came up during the review but were either speculative, already tracked, or too invasive to qualify as "low-hanging":

- `FileEntryViewModel.FormatSize` / `MainShellView.FormatByteSize` duplication — **absorbed by U-5 status-bar cleanup** (the cleanup moves `FormatByteSize` into a VM-shared utility).
- `MainShellView.OnPanePropertyChanged` firing on every pane property change — **absorbed by U-5** (the whole subscription is deleted).
- `MainShellView.UpdateStatusBar` allocating a new `string[]` of size suffixes per call — same as above, U-5.
- Thumbnail byte-buffer pooling — tracked by `SPEC_NUGET_MODERNIZATION.md` §4 / §7; explicitly deferred.
- `CommunityToolkit.HighPerformance.StringPool` for file extensions — tracked by `SPEC_NUGET_MODERNIZATION.md` §3; deferred.
- Any `FileSystemWatcher` tuning — tracked by `SPEC_BUG_FIXES.md` B-2.
- Cancellation re-throw in `NtfsFileIdentityService` — tracked by `SPEC_NATIVE_MODERNIZATION.md` M-2.
- Selection-total-bytes re-enumeration in the status bar — the old shell code-behind path is gone after U-5; the replacement VM computed property can enumerate on demand, deferred to whoever implements it if it turns out to hurt.

## 4. Acceptance

- `NormalizedPath.DisplayPath` reads are O(1) — the struct's backing field is set at construction and never recomputed. Unit tests locking in same-reference semantics pass.
- `FilePaneViewModel.ApplyWatcherBatch` allocates at most one `HashSet<string>` per batch — the existing rename-keeps-selection test still passes after the refactor.
- `FileEntryViewModel.LastWriteTime` / `CreationTime` return a cached string — same reference across reads, formatted with `CultureInfo.InvariantCulture`.
- No regressions in `dotnet test`; `dotnet build -warnaserror` green on Release|x64.

No manual verification is required — all three are invisible to the user except via allocation / CPU profiles.

## 5. Batches

One batch per finding, each very small:

- **P-1.** Memoize `NormalizedPath.DisplayPath`. ~15 LOC net + 4 new unit tests.
- **P-2.** Collapse double `ToHashSet()` in `ApplyWatcherBatch`. ~20 LOC net + 1 regression test.
- **P-3.** Cache `LastWriteTime` / `CreationTime` strings on `FileEntryViewModel`, switch to `CultureInfo.InvariantCulture`. ~30 LOC net + 4 new unit tests.

Handoff notes: `perf-batch-1.md` through `perf-batch-3.md`. Agents may combine P-1 + P-2 into a single batch if the diff stays under 50 LOC net; P-3 stays separate because it touches a different file and has a culture-invariance behavioral change worth reviewing in isolation.

## 6. Non-goals (reiterated for scope enforcement)

- No profiling-driven sweep. This spec is "fixes I can point at", not "things that might be slow".
- No new NuGet dependencies.
- No architectural changes. Every fix is in-place on an existing type.
- No parallelization. Every fix is single-threaded logic.
