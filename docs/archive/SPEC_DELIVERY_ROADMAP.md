# Spec: Delivery Roadmap

Where the project is right now and which specs are authoritative for new work.

## Current state (May 2026)

The first feature wave (UI layout + persistence + tooling + initial native modernization) shipped. After that, the file-entry table control was reworked and the Inspector was refactored; in the process several feature surfaces were dropped because their pre-rework code quality didn't justify carrying them forward. Re-implementation is the active workstream, tracked in `SPEC_LOW_HANGING_IMPROVEMENTS.md`.

## Active specs

| Spec | What it covers | Status |
|---|---|---|
| `SPEC_V1.md` | Product specification | **Authoritative** for product scope |
| `SPEC_LOW_HANGING_IMPROVEMENTS.md` | Active near-term backlog: rewire dropped commands, rich error diagnostics, SRP refactors, stability, memory | **Active** — primary workstream |
| `SPEC_FILE_ENTRY_TABLE_VIEW.md` | The reworked table control's contract (data model, messaging, behaviors, coordinator, dialog service) | **Authoritative** for the table |
| `windows-shell-inspector-high-level-spec.md` | Inspector field catalog, sections, design principles | **Authoritative** for the inspector |
| `windows-shell-inspector-implementation-spec.md` | Inspector category-by-category implementation reference | **Authoritative** for the inspector |
| `winui-file-manager-keyboard-shortcuts-spec.md` | Canonical keyboard shortcut spec, including the gap audit (§18) | **Authoritative** for keyboard |
| `SPEC_LONG_PATHS.md` | Long-path-aware UI gating, capability policy, registry toggle | Pending |
| `SPEC_NUGET_MODERNIZATION.md` | Serilog, HighPerformance pooling | Pending (HighPerformance / Serilog only) |
| `SPEC_TOOLING_AND_ANALYZERS.md` | Analyzer set, CI gates, banned APIs | **Shipped** |
| `SPEC_UI_LAYOUT_AND_RESIZING.md` | Splitters, persistence, status bar | **Shipped** (in-cell rename has since been reverted to dialog-driven) |
| `MEMORY_OPTIMIZATION_RECOMMENDATIONS.md` | Memory analysis playbook | Reference — see `SPEC_LOW_HANGING_IMPROVEMENTS.md` §M for active items |
| `AGENT_BRIEF.md`, `BOOTSTRAP.md`, `CODING_STYLE.md`, `DEVELOPER_TOOLING_GUIDE.md` | Implementation rules and tooling guidance | Reference |
| `native-pr-audit-checklist.md` | One-page PR audit checklist for native code touches | Reference |

## Archived specs

These were authored before the table/inspector rework or have been overtaken by changes in the code. They live under `docs/archive/` and carry an `ARCHIVED` banner at the top. Kept for history; do not act on them without re-validating against current code.

- `archive/SPEC_BUG_FIXES.md` — most tickets target the removed `FilePaneViewModel` watcher pipeline.
- `archive/SPEC_RENAME_BUGS.md` — targeted the removed in-cell rename feature.
- `archive/SPEC_PERF_LOW_HANGING_FRUIT.md` — P-1 still applies (now in improvements §M-1); P-2 / P-3 target removed types.
- `archive/SPEC_AGENT_BATCHING_PLAN.md` — batch list keyed to archived specs.
- `archive/SPEC_NATIVE_MODERNIZATION.md` — does not reflect current code; the surviving idea (cancellation re-throw at native boundaries) is preserved in improvements §St-4.

Two further specs in the active root carry their own `ARCHIVED` banners — they're not yet moved physically:

- `SPEC_FEATURE_LOW_HANGING_FRUIT.md` — feature ideas valid; implementation guidance stale. Re-author against the new architecture if/when picked up.
- `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` — content folded into `winui-file-manager-keyboard-shortcuts-spec.md` §18.

Historical batch handoff notes live under `docs/archive/progress/`.

## Order of work

The active workstream is the suggested order in `SPEC_LOW_HANGING_IMPROVEMENTS.md` §6:

1. Rewire dropped commands (Coordinator + FileOperationDialogService) — gates Copy / Move / Delete with cancellation.
2. Rich error diagnostics for those operations.
3. SRP refactors of the oversized files, in order of pain.
4. Stability + memory passes can interleave anywhere.

`SPEC_LONG_PATHS.md` and the remaining bits of `SPEC_NUGET_MODERNIZATION.md` are picked up after the improvements queue clears.

## What's permanently out of scope

- **Drag-and-drop** between the app and Explorer or any other source/target. Past attempts produced too many edge cases; this is closed for v1 and beyond unless re-opened explicitly.
- Items listed in `SPEC_V1.md` "Out of Scope for v1" remain out of scope: tabs, FTP/SFTP, archives, shell extensions, recycle bin integration, permission editor, ADS editor, etc.
