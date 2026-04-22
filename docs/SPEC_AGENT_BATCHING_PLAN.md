# Spec: Agent Batching Plan

Scope: a prescriptive recipe for slicing each spec in `SPEC_DELIVERY_ROADMAP.md` into agent-sized work units so that (a) no single agent run must hold the entire spec in context, (b) progress is resumable by a fresh agent with only the repo + this plan, and (c) each batch leaves `main` green.

The target agent is Claude running in a context-limited harness. Assume a realistic budget of **~80 k tokens of useful spec + code per turn**. Most specs exceed this when combined with the files they touch. Batching is therefore a first-class correctness tool, not a nice-to-have.

## 1. Definitions

**Batch.** A contiguous chunk of work that:
1. Compiles with `dotnet build -warnaserror`.
2. Passes `dotnet test` (the test suite, not manual checks).
3. Leaves no half-broken feature behind (banned-API suppressions are explicit; dead code is removed, not commented).
4. Lands as one or more commits on a feature branch and merges before the next batch starts.

A batch is *not* a commit — a batch may be 1 to ~5 commits, so long as the pre-merge state satisfies the above.

**Handoff note.** A short Markdown file in `docs/progress/` checked in at the end of each batch. It records what was done, what is next, and any surprises. A fresh agent reads the note + this plan + the originating spec to continue. See §4.

**Checkpoint commit.** The last commit in a batch. Its message is `chore(batch): <spec-id> batch <n>/<total>` and its body references the next batch by id. Checkpoints are the resumption points.

## 2. Universal batching rules

These apply to every spec. Agents must obey them unless explicitly overridden by a spec-specific entry in §3.

1. **One concern per batch.** If a batch is tempted to "also clean up X while here," stop. File a follow-up note instead.
2. **Target ≤ 400 lines of net diff per batch**, excluding generated files. If a batch legitimately needs more (e.g., a single mechanical `[DllImport]` migration to CsWin32), split by file or by API family.
3. **Touch ≤ 6 source files per batch**, excluding the test project. If more, split.
4. **Write or update tests in the same batch as the production code**, not a follow-up. A batch without tests is only acceptable when the spec explicitly declares the surface untestable (e.g., UI resize).
5. **Do not modify specs in a batch.** Spec edits are their own micro-batch, landed first. This prevents agents from "drifting" the acceptance criteria to match their output.
6. **Manual-verification-only work still commits a checklist.** For the UI-layout spec the agent writes down the human-verification results in the handoff note. Missing checklist = batch not complete.
7. **Ban incomplete implementations.** No `TODO: wire the banned-API list later`. Either finish or descope the batch; never both half-done.
8. **Every batch ends on a green CI.** No "will fix in next batch" exceptions.

## 3. Remaining batch plan

Only forward-looking work is listed below. Shipped batches (all of `SPEC_TOOLING_AND_ANALYZERS.md`, N-2b, U-1, U-2, U-3) have been pruned; `docs/progress/` and `git log` are the authoritative record of what's done.

At-a-glance priority:

| Priority | Batch | Spec | Blocking on |
|---|---|---|---|
| **Now** | U-4 wrap-up + U-5 status-bar cleanup | `SPEC_UI_LAYOUT_AND_RESIZING.md` §6, §8, §4.3 | manual acceptance on a workstation |
| **Second** | R-1 … R-3 | `SPEC_RENAME_BUGS.md` | U-4 code already on `master` |
| **Third** | M-1 … M-5 | `SPEC_NATIVE_MODERNIZATION.md` | none — absorbs B3/B9 and NuGet N-1/N-4 |
| **Fourth** | P-1 … P-3 | `SPEC_PERF_LOW_HANGING_FRUIT.md` | none — pure micro-optimizations, each ≤ 50 LOC |
| Then | K-1 | `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §3 | U-4 closed (no code dependency, just sequencing) |
| Then | K-2 | `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §4.1–§4.4 | K-1 landed |
| Then | K-3 | `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §4.6 | K-1 landed (K-2 can interleave) |
| Then | B-1 … B-5 | `SPEC_BUG_FIXES.md` | none — can interleave with K-* or L-* (B3 / B9 absorbed by M-2) |
| Then | N-2 (Serilog) / N-3 (deferred) | `SPEC_NUGET_MODERNIZATION.md` (remainder) | M-4 absorbed §1 (was N-1); M-5 absorbed §2 (was N-4) |
| Then | L-1 … L-5 | `SPEC_LONG_PATHS.md` | M-4 landed (adds `RegNotifyChangeKeyValue`) |
| Ongoing | F-features | `SPEC_FEATURE_LOW_HANGING_FRUIT.md` | L-1 landed for any feature that touches path-gated surfaces |

### 3.1. `SPEC_UI_LAYOUT_AND_RESIZING.md` — U-4 wrap-up + status-bar cleanup (**Now**)

Code for U-4 shipped on `master` (commits `d3bc862`, `113827b`, `35e965f`); automated tests are green. What remains is the manual acceptance pass on a Windows 11 workstation at 100% + 150% DPI, plus a small follow-up polish batch.

- **U-4 wrap-up.** Run §8.4 (in-cell rename, 9 checks), §8.5 (grep for deleted surfaces — already verified in the sandbox: zero hits), §8.6 (inspector still renders), §8.7 (regression over `winui-file-manager-keyboard-shortcuts-spec.md` §17). Back-fill the §8.1 smoothness and §8.2 minimum-width checks that were skipped in `ui-layout-batch-2.md`, and §8.3 restart persistence skipped in `ui-layout-batch-3.md` — they need a GUI environment. On full green, flip `ui-layout-batch-4.md` `Status:` from `in progress` to `complete` and tick the items there.
- **U-5. Status-bar XAML bindings (spec §4.3).** Move the composed status-bar strings from `MainShellView.UpdateStatusBar` into computed read-only properties on `FilePaneViewModel` (`PaneLabel`, `ItemCountDisplay`, `SelectedDisplay`). Add `MainShellViewModel.ActivePaneLabel`. Rebind the status-bar `TextBlock`s via `x:Bind` one-way. Delete `UpdateStatusBar`, the initial call in the constructor, `OnPanePropertyChanged`, the two `PropertyChanged += OnPanePropertyChanged` subscriptions, and the `FormatByteSize` helper. Three small unit tests on the new VM properties. Expected diff: ~200 lines net (mostly movement).
- **(Superseded) U-5 rename error flash.** The previously-planned red `VisualState` flash on the Name TextBox is absorbed by `SPEC_RENAME_BUGS.md` R-2's `InfoBar` banner. Not scheduled.

Handoff notes: `ui-layout-batch-4.md` (exists; flip status on completion), `ui-layout-batch-5.md` (new for the status-bar cleanup).

### 3.2. `SPEC_RENAME_BUGS.md` — 3 batches (**Second**)

The in-cell rename surface from U-4 has three observed defects. This spec fixes them and hardens the commit path against concurrent external writers. All three are code + tests; no manual-only work.

- **R-1. Selection & focus restoration.** Capture the expected destination `NormalizedPath` before awaiting the handler; after success, proactively rewrite the `SourceCache` (remove old + add new) and set `CurrentItem` / `SelectedItem` to the new entry. `ApplyWatcherBatch` consults `_expectedRenameTarget` to suppress the self-rename echo. Two new tests in `ViewModelRenameCommandTests.cs`. Expected diff: ≤ 250 lines.
- **R-2. Collision UX (InfoBar).** Add `FilePaneViewModel.RenameError` (`RenameErrorInfo` record) and `ClearRenameError()`. Map `FileOperationErrorCode` → user-facing messages. Bind an `InfoBar` inside `FilePaneView.xaml` (dismissible, `IsClosable="True"`). Keep editor open with `EditBuffer` intact. Three new tests. Expected diff: ~300 lines.
- **R-3. Race hardening.** Pre-commit `_sourceCache.Lookup` re-check; add `FileOperationErrorCode.SourceGone`; tighten `FileOperationInterop` catches (`FileNotFoundException` / `DirectoryNotFoundException` → `SourceGone`); watcher-suppression so an external delete / rename of `_activeEditingEntry` surfaces as banner rather than silent editor cancellation. Three new tests. Expected diff: ~300 lines.

Handoff notes: `rename-batch-1.md` through `rename-batch-3.md`.

### 3.3. `SPEC_NATIVE_MODERNIZATION.md` — 5 batches (**Third**)

Handle-safety first modernization of the native / interop surface. Absorbs `SPEC_NUGET_MODERNIZATION.md` §1 (was N-1) + §2 (was N-4) and `SPEC_BUG_FIXES.md` B3 + B9. Explicitly defers the HighPerformance / thumbnail work (§3.5 below).

- **M-1. Analyzer enforcement + audit pass.** Add `IDISP001;IDISP003;IDISP004;IDISP007` to `<WarningsAsErrors>`; fix any violations that surface. Author the six-item PR audit checklist in the spec. Expected diff: ≤ 200 lines.
- **M-2. COM RCW + cancellation correctness (absorbs B3, B9).** `Marshal.FinalReleaseComObject` in `FileIdentityInterop.cs`; `Marshal.ReleaseComObject` added to `BannedSymbols.txt`. Explicit `catch (OperationCanceledException) { throw; }` in the seven `NtfsFileIdentityService.*Async` methods. Debug-only STA-thread assertion on shell COM call sites. Expected diff: ~150 lines.
- **M-3. SafeHandle adoption.** Wrap `FindFirstStreamW`'s `IntPtr` in `SafeFindFilesHandle` (prefer CsWin32-generated). Make `IDirectoryChangeStream` `IDisposable` / `IAsyncDisposable`; wire every caller. Expected diff: ~300 lines.
- **M-4. CsWin32 expansion (absorbs NuGet §1 / N-1).** Migrate 15+ hand-rolled `[DllImport]`s in `WindowsShellService.cs`, `NtfsFileIdentityService.cs`, `FileIdentityInterop.cs` into `NativeMethods.txt`. Add `IFileIsInUse`, `RmStartSession/Register/Get/End`, `SHObjectProperties`, `ShellExecuteExW`, `CoInitializeEx`/`CoUninitialize`, `CfGetPlaceholderStateFromAttributeTag`, `RegNotifyChangeKeyValue`. Introduce `ShellInterop`, `RestartManagerInterop`, `CloudFilesInterop` adapter pairs. Split into M-4a (shell/OLE) + M-4b (Restart Manager + Cloud Files + registry) if the diff exceeds 400 lines. Expected total diff: ~600 lines.
- **M-5. CopyFile2 upgrade (absorbs NuGet §2 / N-4).** Route the copy path through `PInvoke.CopyFile2` with `COPYFILE2_CALLBACK_PROGRESS`. Manual check: cancel a 10 GB copy; aborts within 500 ms. Expected diff: ~250 lines.

Handoff notes: `native-batch-1.md` through `native-batch-5.md` (M-4 may span `native-batch-4a.md` + `native-batch-4b.md`).

### 3.4. `SPEC_PERF_LOW_HANGING_FRUIT.md` — 3 batches (**Fourth**)

Small, targeted micro-optimizations in hot paths that came up during the review. Each batch is ≤ 50 LOC net, no new dependencies, invisible to users except via allocation / CPU profiles.

- **P-1. Memoize `NormalizedPath.DisplayPath`.** Compute the display-form string at struct construction and store it in an init-only `DisplayPath` property. Eliminates per-access substring allocation from `UniqueKey`, inspector loaders, and log formatting. ~15 LOC + 4 unit tests.
- **P-2. Collapse double `ToHashSet()` in `FilePaneViewModel.ApplyWatcherBatch`.** Build the selected-paths hash set once and mutate in place for rename reconciliation instead of rebuilding via `.Select(…).ToHashSet()`. ~20 LOC + 1 regression test. May be combined with P-1 in a single batch if the net diff stays under 50 LOC.
- **P-3. Cache `FileEntryViewModel.LastWriteTime` / `CreationTime`.** Compute the formatted strings once in the constructor; switch the format to `CultureInfo.InvariantCulture` to avoid locale-dependent year rendering. Saves allocations on every row realization / inspector read. ~30 LOC + 4 unit tests.

Handoff notes: `perf-batch-1.md` through `perf-batch-3.md`.

### 3.5. `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` — 3 batches

- **K-1. `ShortcutRegistry` + existing shortcuts migrated.** Create `src/WinUiFileManager.Presentation/Input/ShortcutRegistry.cs` per the spec §3, populate with every currently-working shortcut, and refactor `MainShellView.OnPreviewKeyDown` + command-bar tooltips to read from the registry. Zero behavior change. Verify the before/after table in keyboard-spec §17 matches exactly. Expected diff: ~400 lines (mostly a `Default` static populated from existing bindings + one reader site per accelerator).
- **K-2. Missing global shortcuts + keyboard-spec doc edits.** In this order:
  1. Migrate the already-wired `F2` + `Shift+F6` (shipped in U-4) into the registry's `InlineRename` context so they route through the same surface as the others.
  2. Add `Alt+Up` (§4.4), `Alt+Enter` (§4.3), `Shift+Delete` (§4.2) plus the `Ctrl+Home`/`Ctrl+End` correction (§4.5).
  3. Update `winui-file-manager-keyboard-shortcuts-spec.md`: §12.9 heading → "Rename in place (`F2`, `Shift+F6`)" (drop "dialog input" wording); §17 table Rename row → `F2, Shift+F6`; §17 Parent directory row → `Backspace, Ctrl+PageUp, Alt+Up`.
- **K-3. Navigation history + `Alt+Left` / `Alt+Right`.** Add `PaneNavigationHistory` per spec §4.6, expose `GoBackCommand` / `GoForwardCommand` on `FilePaneViewModel`, and add the shell-level key routing. Tests cover the 50-entry cap, forward-stack clearing on non-history navigation, and the inert back/forward at stack boundaries. History is in-memory only — explicit non-goal per the spec.

Handoff notes: `keyboard-batch-1.md` through `keyboard-batch-3.md`.

### 3.6. `SPEC_BUG_FIXES.md` — 5 batches (by severity cluster)

- **B-1. Idle CPU + ShellExecute + cancellation.** B1 only. B2 is subsumed by `SPEC_LONG_PATHS.md` and must not be fixed here. B3 (swallowed `OperationCanceledException`) is absorbed by `SPEC_NATIVE_MODERNIZATION.md` M-2 — if B-1 lands before M-2, skip B3.
- **B-2. Watcher stability.** B4 backoff + B5 sync-over-async removal + B8 async-void setter.
- **B-3. Volume probing.** B6 (volume cache) + B12 (DirectoryExists off UI thread).
- **B-4. Repository and close flow.** B8 JSON per-item failure, B13 double-close race. B9 (`Marshal.ReleaseComObject`) is absorbed by `SPEC_NATIVE_MODERNIZATION.md` M-2 — if B-4 lands before M-2, skip B9.
- **B-5. Cleanup.** B10 NavigateUp edge case, B11 clipboard off UI thread, B14 inspector LINQ.

Every batch writes regression tests where the spec calls for them.

Handoff notes: `bugs-batch-1.md` through `bugs-batch-5.md`.

### 3.7. `SPEC_NUGET_MODERNIZATION.md` (remainder) — 2 batches

§1 (CsWin32 expansion, was N-1) and §2 (CopyFile2 upgrade, was N-4) were absorbed by `SPEC_NATIVE_MODERNIZATION.md` M-4 / M-5. Only the following remain:

- **N-2. Serilog (§5).** Wire the logger in `ServiceConfiguration`; file + async + rolling sinks.
- **N-3. HighPerformance + thumbnail pooling (§3, §4/§7).** `StringPool` for extensions; `ArrayPool<byte>.Shared` for thumbnail copy. Deferred per user direction — schedule only after features spec Sprint A or on explicit request.

Handoff notes: `nuget-batch-2.md`, `nuget-batch-3.md`.

### 3.8. `SPEC_LONG_PATHS.md` — 5 batches

- **L-1. Domain + capability policy.** Add `PathLength`, `PathCapability`, `IPathCapabilityPolicy`, `DefaultPathCapabilityPolicy`. No consumers yet.
- **L-2. Service guards.** Wire `WindowsShellService`, `NtfsFileIdentityService` (Thumbnail / Cloud / Locks); add the `Unsupported` sentinels. Subsumes bug B2: `OpenWithDefaultAppAsync` uses `DisplayPath` and is gated.
- **L-3. UI disable + tooltips + LONG chip.** `CanExecute` plumbing on every affected `[RelayCommand]`; per-pane LONG chip; tooltip switching.
- **L-4. Inspector batch gating.** Per-batch `RequiredCapability`; the Inspector orchestrator emits `Unsupported`; `"Unavailable (extended-length path)"` rendering.
- **L-5. Long-paths toolbar toggle + `RegNotify`.** `ILongPathsEnvironment` with a `Changed` event via `RegNotifyChangeKeyValue`; the toolbar `AppBarToggleButton`; `SetEnabledAsync` via elevated `reg.exe`. Requires M-4's `RegNotifyChangeKeyValue` import.

Handoff notes: `long-paths-batch-1.md` through `long-paths-batch-5.md`.

### 3.9. `SPEC_FEATURE_LOW_HANGING_FRUIT.md` — one batch per feature

Each feature `F1..F15` (minus `F11`, which is already delivered by spec 3; and `D1` favourites-popup polish, which is deferred per the roadmap) is its own batch. The feature spec already defines scope per feature; agents do not further split unless a feature exceeds the §2 limits. Start with Sprint A (F1 quick filter, F4 reveal-in-Explorer / terminal-here, F15 hidden toggle) per the feature spec's sprint plan.

Note: the `F4` key *binding* to an external editor (`SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §5.3) is deferred and is a distinct item from the `F4` *feature* in this spec — the feature ships; the key binding does not.

## 4. Handoff-note format

Every batch checkpoint writes `docs/progress/<spec-id>-batch-<n>.md` with this template:

```markdown
# Batch <n> of <total>: <short title>

**Spec:** `SPEC_XXX.md` §<sections covered>
**Branch merged into main:** <branch name>
**Status:** complete · blocked · rolled back

## What shipped
- <1-5 bullets, each naming a feature/fix and citing file:line of the key change>

## What's next
- <1-3 bullets — the next batch's scope, in terms specific enough that a fresh
  agent doesn't have to re-read the full spec>

## Acceptance results
<copy-paste the spec's acceptance checklist for this batch, with [x] / [ ] /
[skipped — <reason>] in front of each item>

## Surprises
<anything that deviated from the spec: API shapes that didn't exist, analyzer
rule overrides, unexpected test failures, perf numbers the spec didn't
anticipate. Each surprise becomes a candidate for a spec edit on the next turn>

## Context hints for the next agent
<files the next batch will touch, key types involved, any load-bearing
invariants that aren't obvious from the spec>
```

Keep each note under 200 lines. Longer notes are a smell — either the batch was too big or the agent is re-explaining the spec.

## 5. Starting a fresh agent mid-roadmap

When a new agent begins mid-roadmap, it reads, in order:

1. `docs/SPEC_DELIVERY_ROADMAP.md` — where in the plan we are.
2. `docs/progress/` — the most recent handoff note for the relevant spec.
3. `docs/SPEC_<the spec being worked on>.md` — but only the sections cited in the handoff note's "What's next".
4. The `AGENT_BRIEF.md` and `CODING_STYLE.md` — baseline rules, short.

The agent **does not re-read** prior handoff notes for the same spec unless the current note's "Surprises" section points to them. This keeps context bounded.

If `docs/progress/` is empty, look at §3 — the topmost entry is the active batch.

### 5.1. Current resumption point

The active batch is **U-4 (in-cell rename)** — see §3.1. Code is on `master`; what remains is the §8.4 manual verification pass, after which `ui-layout-batch-4.md` flips from `in progress` to `complete`. The chain after U-4 closes:

1. **U-5** status-bar XAML cleanup (§3.1) — same spec.
2. **R-1 → R-2 → R-3** (`SPEC_RENAME_BUGS.md`, §3.2) — fix the three rename defects.
3. **M-1 → M-2 → M-3 → M-4 → M-5** (`SPEC_NATIVE_MODERNIZATION.md`, §3.3) — handle-safety modernization.
4. **P-1 → P-2 → P-3** (`SPEC_PERF_LOW_HANGING_FRUIT.md`, §3.4) — three micro-optimizations.
5. **K-1** (`SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §3, §3.5) — `ShortcutRegistry` migration.
6. Then K-2, K-3, B-1…B-5, N-2, L-1…L-5, features.

A fresh agent should read the topmost in-progress handoff note (`ui-layout-batch-4.md` today) plus the section of this plan that names their current batch.

## 6. Red-flag signals — stop and ask

An agent should stop and ask a human if any of the following happen mid-batch:

- The acceptance checklist for the current batch references a file or type that does not exist in the repo. Likely cause: an earlier batch left off half-done.
- A banned-API violation cannot be cleanly routed through the approved service layer. Likely cause: the capability matrix needs extension.
- More than one batch's worth of refactoring is needed to land the current batch without breaking the build. Likely cause: the spec's cut points were wrong and need a re-plan.
- Manual verification steps produce a different result than the spec predicts (e.g., a bug-fix batch's acceptance test still reproduces the bug). Likely cause: the root-cause analysis in the spec was incomplete.

Ask by writing the question into the handoff note under "Surprises" and stopping the batch at its last green commit. Do not proceed.

## 7. What this plan is *not*

- Not a project-management tool. No dates, no owners.
- Not a replacement for the specs. Agents still follow the specs to the letter; the plan only slices them.
- Not a CI/CD document. CI enforcement is in `SPEC_TOOLING_AND_ANALYZERS.md`; this plan assumes CI exists and runs on every batch.

## 8. Acceptance

This plan is followed correctly when:

- Every batch listed in §3 produces a corresponding handoff note in `docs/progress/` when it lands. (Historical batches that shipped before this plan became strict are recorded in `git log`; new batches always write notes.)
- No batch has more than ~400 net diff lines (excluding generated files) without a cited justification in its note.
- `main` is green on every checkpoint commit.
- A fresh agent can continue the roadmap given only `SPEC_DELIVERY_ROADMAP.md`, this plan, the latest handoff note, and the repo. No human Q&A required.
