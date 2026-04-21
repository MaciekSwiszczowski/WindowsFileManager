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

## 3. Per-spec batch plans

Each entry gives a batch count, the cut points, and the handoff-note filename for each. Filenames follow `docs/progress/<spec-id>-batch-<n>.md`.

### 3.1. `SPEC_TOOLING_AND_ANALYZERS.md` — 3 batches

- **T-1.** Add analyzer packages + `Directory.Build.props` wiring + `.editorconfig`. No `BannedSymbols.txt` yet. Fix existing `WarningsAsErrors`-level violations (a handful of `CA2007` / `VSTHRD100` hits). Expected diff: ~250 lines.
- **T-2.** `BannedSymbols.txt` with the full list from the spec. File-scoped `#pragma warning disable RS0030` on the legitimate exception sites (`FileOperationInterop` for `File.Copy`, CsWin32-driven `DllImport`, and `RxSchedulerProvider` for the one allowed `SynchronizationContext.Current` capture). Expected diff: ~150 lines, mostly suppression pragmas.
- **T-3.** `InternalsVisibleTo` + visibility narrowing pass. `LoggerMessage`-generated loggers for the hottest 3 log callsites (`FilePaneViewModel`, `WindowsFileSystemService`, `WindowsFileOperationService`). DI validation (`ValidateOnBuild`). Expected diff: ~300 lines.

Handoff notes: `tooling-batch-1.md`, `tooling-batch-2.md`, `tooling-batch-3.md`.

### 3.2. `SPEC_NUGET_MODERNIZATION.md` §2b — 1 batch

- **N-2b.** Add `CommunityToolkit.WinUI.Controls.Sizer` to `Directory.Packages.props` and `WinUiFileManager.Presentation.csproj`. No consumers yet — that's spec 3's job. Verify `dotnet build` still succeeds. Expected diff: ~15 lines.

### 3.3. `SPEC_UI_LAYOUT_AND_RESIZING.md` — 4 batches

- **U-1. Layout skeleton.** Replace the `MainShellView.xaml` column set with the 5-column plan from §3.1 (Left=pixel, Sizer, Right=*, Sizer, Inspector=pixel). Bind `LeftPaneColumn.Width` and `InspectorColumn.Width` to VM properties via a new `PixelGridLengthConverter`. Delete the hand-rolled splitter handlers. Do not touch persistence yet.
- **U-2. Inspector cascade cleanup.** Remove `InspectorContentWidth` and per-category `ContentWidth`. Update XAML to stretch naturally. Run manual splitter smoothness check §8.1; record result in the handoff.
- **U-3. Persistence rollout.** Extend `AppSettings` with the new fields, update `PersistPaneStateCommandHandler`, wire load/save through `MainShellViewModel.InitializeAsync` and `PersistStateAsync`. Include main-window placement. Run manual persistence check §8.3.
- **U-4. In-cell rename.** Delete `IDialogService.ShowRenameDialogAsync`; add `IsEditing` / `EditBuffer` on `FileEntryViewModel`; swap the Name column to `TableViewTemplateColumn`; add editor key handlers; wire F2. Run manual rename checklist §8.4.

Handoff notes: `ui-layout-batch-1.md` through `ui-layout-batch-4.md`.

### 3.4. `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` — 3 batches

- **K-1. ShortcutRegistry + existing shortcuts migrated.** Create the registry and migrate every currently-working shortcut into it. Zero behavior change. Extensive before/after verification against the existing keyboard spec §17 table.
- **K-2. New shortcuts: F2, Shift+F6 (routed to in-cell), Alt+Up, Alt+Enter, Shift+Delete.** Depends on UI spec batch U-4.
- **K-3. Navigation history + Alt+Left / Alt+Right.** Adds `PaneNavigationHistory`, the commands on `FilePaneViewModel`, and the shell-level key routing. Tests cover the history stack mechanics (not UI).

Handoff notes: `keyboard-batch-1.md` through `keyboard-batch-3.md`.

### 3.5. `SPEC_BUG_FIXES.md` — 5 batches (by severity clusters)

- **B-1. Idle CPU + ShellExecute + cancellation.** B1, B3. B2 is deferred to spec 7 (long paths); do not fix here.
- **B-2. Watcher stability.** B4 backoff + B5 sync-over-async removal + B8 async-void setter.
- **B-3. Volume probing.** B6 (volume cache) + B12 (DirectoryExists off UI thread).
- **B-4. Repository and close flow.** B8 JSON per-item failure, B9 ReleaseComObject, B13 double-close race.
- **B-5. Cleanup.** B10 NavigateUp edge case, B11 clipboard off UI thread, B14 inspector LINQ.

Every batch writes regression tests where the spec calls for them.

Handoff notes: `bugs-batch-1.md` through `bugs-batch-5.md`.

### 3.6. `SPEC_NUGET_MODERNIZATION.md` (remainder) — 4 batches

- **N-1. CsWin32 expansion (§1).** Move every `[DllImport]` from `NtfsFileIdentityService`, `WindowsShellService`, `FileIdentityInterop` into `NativeMethods.txt`. Split into two sub-batches if diff exceeds 400 lines: shell/OLE APIs first, then Restart Manager + Cloud Files. The spec lists the exact additions.
- **N-2. Serilog (§5).** Wire the logger in `ServiceConfiguration`; sinks + rolling + async.
- **N-3. HighPerformance + thumbnail pooling (§3, §4/§7).** `StringPool` for extensions; `ArrayPool<byte>.Shared` for thumbnail copy.
- **N-4. CopyFile2 upgrade (§2).** Progress callback wiring. Manual check: cancel a 10 GB copy; aborts within 500 ms.

Handoff notes: `nuget-batch-1.md` through `nuget-batch-4.md`.

### 3.7. `SPEC_LONG_PATHS.md` — 5 batches

- **L-1. Domain + capability policy.** Add `PathLength`, `PathCapability`, `IPathCapabilityPolicy`, `DefaultPathCapabilityPolicy`. No consumers yet.
- **L-2. Service guards.** Wire `WindowsShellService`, `NtfsFileIdentityService` (Thumbnail / Cloud / Locks), add the `Unsupported` sentinels. Subsumes bug B2 here: `OpenWithDefaultAppAsync` uses `DisplayPath` and is gated.
- **L-3. UI disable + tooltips + LONG chip.** CanExecute plumbing on all affected `[RelayCommand]`s; per-pane LONG chip; tooltip switching.
- **L-4. Inspector batch gating.** Per-batch `RequiredCapability`; Inspector orchestrator emits `Unsupported`; `"Unavailable (extended-length path)"` rendering.
- **L-5. Long-paths toolbar toggle + RegNotify.** `ILongPathsEnvironment` with `Changed` event via `RegNotifyChangeKeyValue`; the toolbar `AppBarToggleButton`; `SetEnabledAsync` via elevated `reg.exe`. Requires the CsWin32 additions from N-1 (`RegNotifyChangeKeyValue`).

Handoff notes: `long-paths-batch-1.md` through `long-paths-batch-5.md`.

### 3.8. `SPEC_FEATURE_LOW_HANGING_FRUIT.md` — one batch per feature

Each feature `F1..F15` (minus `F11` which is subsumed and `F4` external-editor which is deferred) is its own batch. The feature spec already defines the scope per feature; agents do not further split unless a feature exceeds the §2 limits.

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

If `docs/progress/` is empty, the agent starts at batch #1 of spec #1 (tooling).

## 6. Red-flag signals — stop and ask

An agent should stop and ask a human if any of the following happen mid-batch:

- The acceptance checklist for the current batch references a file or type that does not exist in the repo. Likely cause: an earlier batch left off half-done.
- A banned-API violation cannot be cleanly routed through the approved service layer. Likely cause: the capability matrix needs extension.
- More than one batch's worth of refactoring is needed to land the current batch without breaking the build. Likely cause: the spec's cut points were wrong and need a re-plan.
- Manual verification steps produce a different result than the spec predicts (e.g., splitter still slow after U-1 and U-2). Likely cause: the root-cause analysis in the spec was incomplete.

Ask by writing the question into the handoff note under "Surprises" and stopping the batch at its last green commit. Do not proceed.

## 7. What this plan is *not*

- Not a project-management tool. No dates, no owners.
- Not a replacement for the specs. Agents still follow the specs to the letter; the plan only slices them.
- Not a CI/CD document. CI enforcement is in `SPEC_TOOLING_AND_ANALYZERS.md`; this plan assumes CI exists and runs on every batch.

## 8. Acceptance

This plan is followed correctly when:

- Every spec in `SPEC_DELIVERY_ROADMAP.md` has at least one corresponding handoff note per batch in `docs/progress/`.
- No batch has more than ~400 net diff lines (excluding generated files) without a cited justification in its note.
- `main` is green on every checkpoint commit.
- A fresh agent can continue the roadmap given only the roadmap, this plan, the latest handoff note, and the repo. No human Q&A required.
