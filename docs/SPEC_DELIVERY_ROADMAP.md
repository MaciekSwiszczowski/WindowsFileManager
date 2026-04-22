# Spec: Delivery Roadmap

Short, prescriptive order in which the modernization and fix specs land. The rationale column exists so the order is reviewable — if the rationale breaks, the order is free to change.

Companion document: `SPEC_AGENT_BATCHING_PLAN.md` describes how to slice each spec into agent-sized work units.

## Order

| # | Spec | Status | Why here |
|---|---|---|---|
| 1 | `SPEC_TOOLING_AND_ANALYZERS.md` | **shipped** | Adds Meziantou, VS Threading, Roslynator, IDisposableAnalyzers, AsyncFixer, BannedApiAnalyzers, `.editorconfig`, `WarningsAsErrors`. Every later spec produces code that benefits from the static-analysis guardrails. `BannedSymbols.txt` also encodes several bug-fix rules — catching regressions for free. |
| 2 | `SPEC_NUGET_MODERNIZATION.md` §2b (Sizers only) | **shipped** | Pull just the `CommunityToolkit.WinUI.Controls.Sizers` package reference forward because the next spec depends on it. Rest of the NuGet spec is deferred to slot 6. (Package exports both `Sizer` and `GridSplitter`; spec 3 uses `GridSplitter`.) |
| 3 | `SPEC_UI_LAYOUT_AND_RESIZING.md` | **complete (U-1…U-5 shipped)** | Splitter performance, panel/inspector resizing with min widths, persistence of widths + column layout + sort + window placement, in-cell rename replacing the modal dialog, and status-bar XAML cleanup. Manual acceptance is complete. The shipped splitter design uses `GridSplitter` for resize behavior plus lightweight pointer-start / pointer-end handlers to freeze both `FileTable` controls during drag. |
| 4 | `SPEC_RENAME_BUGS.md` | pending | Hardens the newly-shipped in-cell rename surface before broader keyboard or native work builds on top of it. Depends on spec 3 being fully accepted. |
| 5 | `SPEC_NATIVE_MODERNIZATION.md` | pending | Handle-safety modernization of the native / interop boundary. Absorbs parts of the NuGet and bug-fix specs and reduces risk before more feature work lands. |
| 6 | `SPEC_PERF_LOW_HANGING_FRUIT.md` | pending | Small post-modernization micro-optimizations with tight scope and low review cost. |
| 7 | `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` | pending | Introduces the central `ShortcutRegistry` and adds the 7 missing shortcuts (F2 rename, Shift+F6 corrected, Alt+Up, Alt+Left, Alt+Right, Alt+Enter, Shift+Delete). F2 / Shift+F6 wiring is already in place from spec 3. |
| 8 | `SPEC_BUG_FIXES.md` | pending | Bug tickets B1–B14. Most of them become trivial once the analyzers from spec 1 are in place (the analyzers already flag the same patterns). Item B2 (ShellExecute `\\?\` path) is subsumed by spec 10 below; all others ship here. |
| 9 | `SPEC_NUGET_MODERNIZATION.md` (everything except §2b, already landed) | pending | CsWin32 expansion remainder, Serilog, `CommunityToolkit.HighPerformance`, CopyFile2 upgrade, thumbnail-byte pooling, DynamicData audit. Some items are absorbed by spec 5; the rest can interleave with spec 10 since they touch different files. |
| 10 | `SPEC_LONG_PATHS.md` | pending | Path-capability model, service guards, UI disable with tooltips, long-paths toolbar toggle button. Depends on CsWin32 expansion from spec 9 / spec 5 (`RegNotifyChangeKeyValue` additions). Subsumes bug B2. |
| 11 | `SPEC_FEATURE_LOW_HANGING_FRUIT.md` | pending (F11 already delivered by spec 3) | Feature work starts only after the infrastructure above is in place. Deliver feature-by-feature per the feature spec's own sprint plan; treat D1 (Favourites popup) as deferred. |

## Gating rules

Each spec's PR set must not merge until the prior spec's acceptance checklist is fully green. Exception: specs marked "concurrent with" above may overlap if the touched files are disjoint.

- Spec 1 gates the whole chain: no `WarningsAsErrors` → analyzers not enforced → every subsequent spec ships without guardrails.
- Spec 3 gates spec 4: rename hardening starts only after the UI-layout spec is fully accepted.
- Spec 5 / spec 9 (CsWin32 expansion) gate spec 10's `RegNotifyChangeKeyValue` usage.
- Spec 11 (features) does not gate anything — treat it as open-ended backlog.

## Parallelism opportunities

If more than one agent is working, these pairs can run in parallel on separate branches:

- Spec 4 (keyboard registry) and the later items of spec 3 (persistence rollout).
- Spec 5 (bug fixes B3/B4/B5/B6/B7/B8) and spec 6 (Serilog + CsWin32 expansion).
- Spec 7 long-paths toggle button and spec 8 Sprint A features (F1 quick filter, F15 hidden toggle).

Conflicts to avoid:
- Do not parallel spec 3 and spec 4 at the cell-template level — both edit `FileEntryTableView.xaml`.
- Do not parallel spec 7 and spec 5 B2 — spec 7 subsumes B2.

## Deferred, not on the roadmap

- `MISSING_FEATURES_SPEC.md` §4 — Favourites management surface. De-prioritized.
- `SPEC_FEATURE_LOW_HANGING_FRUIT.md` D1 — Favourites popup polish. Same.
- `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §5.3 — F4 external editor.
- Clipboard file-ops (Ctrl+C/X/V). No spec yet.
- `F3` viewer, archive support, treemap, etc. — see feature spec non-goals.

Revisit any of these when the roadmap above is complete or the human owner signals a priority change.

## Done criteria for the roadmap

The roadmap itself is "done" when specs 1–7 are all green per their own acceptance. Spec 8 is ongoing feature work and does not gate "roadmap completion". At that point:

- The app has smooth splitters, persisted layout, in-cell rename, 7 new keyboard shortcuts, structured logging, CopyFile2 with cancellation, analyzer-gated CI, banned-API enforcement, and long-path first-class support (including the toolbar toggle).
- Native-memory growth is bounded (covered in spec 7 indirectly via `StorageFile`/`StorageFolder` disposal).
- The 100 000-files-per-folder target is met (covered in the performance sections of bug-fix spec P1.*).
