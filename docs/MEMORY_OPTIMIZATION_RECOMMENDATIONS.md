# Memory Optimization Recommendations

Reference playbook for memory-related work on this codebase. The diagnosis approach (§2), runtime knobs (§6), virtualization rules (§7), publishing options (§8), the rejected list (§9), and the testing strategy (§10) are evergreen. Specific code-level recommendations have been pulled into `SPEC_LOW_HANGING_IMPROVEMENTS.md` §M-1..§M-5 as actionable items.

This is a developer-oriented cheat sheet of what to do, what to skip, and in what order.

## 1. Context and baseline

- **The WinUI 3 + Windows App SDK baseline is not free.** A blank WinUI 3 window is typically **150–250 MB of private bytes** before your app renders anything, due to DirectX, DComp, `Microsoft.UI.Xaml`, `CoreMessaging`, Skia, ICU, and font tables. No amount of managed-side optimization will get you below that floor.
- **Per-entry cost on this codebase works out to ~5 MB per 10 000 items** in the post-rework architecture (one `SpecFileEntryViewModel` thin wrapper per row + the underlying `FileSystemEntryModel` record + path strings). The rest of the ~50 MB managed heap is framework/runtime/JIT/metadata that does not scale with folder size.
- **Managed memory not shrinking after leaving a folder is most likely workstation-GC high-water, not a leak.** .NET does not eagerly return committed heap to the OS. Under workstation concurrent GC (the default for a UI app, confirmed in `WinUiFileManager.App.runtimeconfig.json` — no `System.GC.Server` property), Gen2 runs on its own schedule and the committed segments are retained.
- **Slow but constant growth *across* navigations, however, is a retention pattern**, not a high-water effect. Diagnose it before committing to any refactor.

## 2. Diagnose first — compacting-Gen2 measurement

Before any refactor is trusted to "fix" memory, add a debug command (e.g. `Ctrl+Shift+G`) that runs:

```csharp
GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
GC.WaitForPendingFinalizers();
GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
var bytes = GC.GetTotalMemory(forceFullCollection: true);
```

Log `bytes` somewhere visible (status bar is fine). Then:

1. Navigate into a 10 000-file folder → record `X`.
2. Navigate into a small folder → record `Y`.
3. Navigate back into the 10 000-file folder → record `Z`.

Interpretation:

- `X ≈ Z` and `Y ≪ X` → **no leak.** The symptom is workstation-GC high-water. Apply the "runtime & GC" fixes below.
- `Z > X + ~1 MB` consistently → **real retention.** Use dotMemory to diff snapshots between (1) and (3); the leak will surface as a single type with an unexpected count.

Do not skip this step. Every section below makes more sense once you know which category you are in.

## 3. Architectural notes already in place

The post-rework table architecture is already memory-conservative by construction:

- `SpecFileEntryViewModel` is a 19-line wrapper that holds one reference (`FileSystemEntryModel? Model`) plus a parent-row flag. It does not derive from `ObservableObject`, has no `PropertyChanged`, and does not cache derived display strings on the row VM. Display formatting happens on demand in cell templates via `SpecFileEntryDisplay` and converters.
- `WinUI.TableView` inherits `ListView`, which uses `ItemsStackPanel` by default. The XAML overrides the items panel where needed; do not add a `VirtualizingStackPanel.VirtualizationMode="Recycling"` attribute — `WinUI.TableView` ignores it.
- `CacheLength="1.0"` cuts realized-row count 3–4× versus the default `CacheLength=4.0`. Verify the live XAML still has this when touching the table layout.
- The `Name` cell template uses `{x:Bind}` with `x:DataType="vm:SpecFileEntryViewModel"`, removing reflection on each row realization. Other `TableViewTextColumn` entries stay on `{Binding}` because `TableViewBoundColumn.Binding` is a `BindingBase` property and cannot accept compiled bindings.

These are *property of the architecture*, not separate workstreams; preserve them when refactoring.

## 4. Per-entry allocation wins

Tracked as active items in `SPEC_LOW_HANGING_IMPROVEMENTS.md` §M-1..§M-3. Repeated here for the callsite analysis:

- **Memoize `NormalizedPath.DisplayPath`** (`Domain/ValueObjects/NormalizedPath.cs`). Currently allocates a substring on every access. `DisplayPath` is read by every cell binding that surfaces the path, every `SourceCache.AddOrUpdate` keyed by path, every Inspector field load, every log line. Compute once in the constructor and store alongside `Value`.
- **Drop `NtfsFileId` from the enumeration-path `FileSystemEntryModel`.** It is always `NtfsFileId.None` during enumeration; only the Inspector populates it lazily. Removing the field shrinks the record and removes a confusing always-empty value.
- **Intern file extensions during enumeration.** 10k `.txt` files currently allocate 10k distinct extension strings. A `Dictionary<string, string>` cache in the enumeration path collapses them to one. Bounded growth: extensions per pane are few.
- **Skip the double path allocation in `WindowsFileSystemService.BuildEntryModel(ref FileSystemEntry)`.** Today the build first calls `entry.ToFullPath()` (one allocation), then `NormalizedPath.FromUserInput` (a second allocation that reapplies the `\\?\` prefix and trims). Build the `NormalizedPath` directly from the span.

## 5. Runtime & GC settings

- **Post-navigation compacting Gen2 GC**, gated on item count (e.g. when loaded count > 1000):

  ```csharp
  GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: true);
  GC.WaitForPendingFinalizers();
  GC.Collect(2, GCCollectionMode.Optimized, blocking: true, compacting: true);
  ```

  Pays a 20–80 ms pause once per big navigation to reclaim the old VMs and actually shrink the managed heap. Defensible for known large transient workloads.

- **Set `"System.GC.RetainVM": false`** in `runtimeconfig.json` (or `<RetainVMGarbageCollection>false</RetainVMGarbageCollection>` in the csproj). Tells the GC to decommit unused segments rather than hold them paged-out but reserved. Makes private bytes actually drop after Gen2.

- **Do NOT switch to server GC.** Server GC creates one heap per core, hurts the baseline, and worsens pause characteristics for a UI app. Workstation concurrent GC is the correct mode here.

## 6. TableView virtualization

- **Keep virtualization ON.** Disabling it would realize ~10k rows at ~10–30 KB each ≈ **100–300 MB of visual tree**, plus O(N) initial layout. Virtualization-off is viable up to a few hundred rows; catastrophic at 10 000.
- **`CacheLength="1.0"` is the right knob.** Reduces realized rows 3–4× versus default. Verify still applied after touches.
- **Prefer `{x:Bind Mode=OneTime}` over `{Binding}`** in `DataTemplate`s. Only `TableViewTemplateColumn` supports `x:Bind`; `TableViewTextColumn.Binding` cannot take a compiled binding.
- **Optional: `TableViewDateColumn`** bound directly to `Model.LastWriteTimeUtc`. Trades per-entry string storage for per-render formatting. Good if memory dominates; bad if scroll CPU dominates. Verify `DateFormat` supports `.NET` patterns before committing.

## 7. Publishing and runtime footprint

Most measurements happen on **Debug x64** builds — the highest-cost configuration.

| Option | Private bytes delta | Managed heap delta | Effort / risk |
| --- | --- | --- | --- |
| Debug → Release | **–50 to –100 MB** | ~–5 MB | free |
| Release + `PublishReadyToRun` | –5 to –15 MB | unchanged | low; larger on-disk |
| Release + `PublishTrimmed` | –10 to –30 MB | ~unchanged | real testing effort |
| Native AOT | n/a | — | not yet viable for WinUI 3 |

Recommended sequence:

1. Measure in **Release** first. Most of what you hope trimming gives you may already be there.
2. Add **R2R** next — low risk, purely a CPU / JIT win.
3. Try **trimming** only if (1) and (2) are insufficient. Caveats for this codebase:
   - `{Binding}` uses reflection on property paths; any surviving `{Binding}` on the TableView must be preserved via trim attributes, or migrated to `{x:Bind}` where possible.
   - `System.Text.Json` default (reflection) is NOT trim-safe. Check `JsonSettingsRepository` and `JsonFavouritesRepository`; switch to source-generated `JsonSerializerContext` if trimming is enabled.
   - Start with `<TrimMode>partial</TrimMode>`; only move to `full` after a full UI smoke.
4. **Native AOT is not yet practical for WinUI 3 apps.** Do not attempt.

## 8. Explicitly deferred / rejected

- **Disable virtualization.** See §6. Catastrophic at 10k rows.
- **Pool row VM instances.** Pooling optimizes allocation throughput, not retention. The symptom is retention. The current `SpecFileEntryViewModel` is already allocation-cheap (no `PropertyChanged`, two reference fields), so pooling has nothing left to win.
- **5-minute folder cache / VM cache.** Conflicts directly with the stated memory goal. The expensive thing is not VM construction; it is directory enumeration syscalls and first-layout, neither of which a VM cache addresses. Cache invalidation under concurrent file-system changes is also non-trivial. If re-navigation speed turns out to be a real UX problem (which has not been demonstrated), cache `IReadOnlyList<FileSystemEntryModel>` keyed on `NormalizedPath`, not VMs.
- **Migrate to R3 (Cysharp) from Rx.NET.** R3 reduces per-emission allocation cost; it does not change retention semantics. The symptom is retention. Not worth the migration cost.
- **Server GC.** See §5.

## 9. Testing strategy

Two tiers of tests, each catching what the other misses.

### Tier 1 — BenchmarkDotNet project (manual, not CI)

- New project: `tests/WinUiFileManager.Benchmarks` (console exe).
- Packages: `BenchmarkDotNet`, `BenchmarkDotNet.Diagnostics.Windows`.
- Decorate benchmarks with both `[MemoryDiagnoser]` and `[NativeMemoryProfiler]`.
- Benchmarks to add:
  - `Enumerate10kFiles`
  - `Build10kEntryModels`
  - `LoadNtfsMetadata_100Files`
  - `ResolveLockDiagnostics_100Files`
  - `LoadThumbnails_100Files`
- Use `[IterationSetup]` / `[IterationCleanup]` to create and tear down the temp-file tree per iteration. Keep setup out of the measured window.
- Run locally, with Administrator (ETW requirement). Commit a baseline file; diff before merging interop changes.
- `[ShortRunJob]` is sufficient for leak checks (3 × 3 iterations). The signal is "allocations match frees," not statistical rigor.

### Tier 2 — Debug-counter assertions (CI)

Pattern:

```csharp
internal static class DebugCounters
{
    private static int _findFilesHandles;
    [Conditional("DEBUG")] public static void FindHandleCreated() => Interlocked.Increment(ref _findFilesHandles);
    [Conditional("DEBUG")] public static void FindHandleReleased() => Interlocked.Decrement(ref _findFilesHandles);
    public static int FindHandleCount => Volatile.Read(ref _findFilesHandles);
}
```

Hit the counters from `SafeFindFilesHandle.ReleaseHandle` and every RCW wrapper's dispose path. Assert zero in test teardown:

```csharp
[Test]
public async Task Enumerate1000Folders_ReleasesAllHandles()
{
    await ExerciseEnumerations(count: 1000);
    GC.Collect(2, GCCollectionMode.Aggressive);
    GC.WaitForPendingFinalizers();
    Assert.That(DebugCounters.FindHandleCount, Is.EqualTo(0));
}
```

Runs on every build. Catches COM-ref and `HANDLE` leaks that ETW cannot see.

### Tier 3 — One-off under Application Verifier

When something smells wrong, run a manually-driven UI session under Application Verifier with PageHeap + Handle leak checks. Not automated; not continuous. Catches exotic stuff (double-free, uninitialized `HANDLE`, GDI leaks).

### Important caveat

When the symptom is **managed memory growth**, `[NativeMemoryProfiler]` is the wrong instrument; `[MemoryDiagnoser]` + dotMemory snapshots are. Do not let the native-memory test infrastructure distract from the actual bug hunt.

## 10. Suggested order of operations

1. Build **Release** and re-measure the 10k-folder scenario. Record new baseline.
2. Add the debug "measure managed memory after Gen2" command. Run the `X / Y / Z` test from §2.
3. If (2) shows a real leak, dotMemory snapshot diff. Fix the specific retention.
4. Land the §4 items as their own small PRs (improvements §M-1..§M-3 and the path-allocation skip).
5. Apply §5 (`RetainVM=false`, post-nav compacting GC).
6. Add §9 testing infrastructure.
7. Revisit §7 trimming only if still unsatisfied.

Every step is independently valuable and reversible. None depends on the next.
