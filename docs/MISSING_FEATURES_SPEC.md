# Missing Features Spec

## Purpose
This document defines the near-term features that are still missing from the implemented UI and should be delivered next.

This scope explicitly excludes advanced copy/move option flows.

## In Scope

### 1. Live Operation Progress Surface
Add a real-time progress UI for long-running copy/move/delete operations.

Required behavior:
- show operation type, processed count, total count, and current item path
- support cancel while operation is running
- keep UI responsive while progress is shown
- show progress using a modal operation dialog (same dialog-service flow used for operation summaries), not as a permanent shell/pane surface
- show final result summary after completion/cancel/failure in the existing operation result dialog

Acceptance:
- progress appears during long operations (not only after completion)
- cancel request is routed and reflected in final summary
- no always-visible progress container is added between command bar and panes

### 2. Centralized Shortcut Registry — DELIVERED BY `SPEC_KEYBOARD_SHORTCUTS_GAPS.md`

Subsumed by `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` §3 (`ShortcutRegistry`). The registry is the single source of truth for command-to-shortcut mappings and drives tooltip generation. Close this item here; track remaining work in that spec.

### 3. Persistence Enhancements — DELIVERED BY `SPEC_UI_LAYOUT_AND_RESIZING.md`

Subsumed by `SPEC_UI_LAYOUT_AND_RESIZING.md` §5 (persistence). Left-pane width, inspector width, per-pane column layout, per-pane sort, and main-window placement are all persisted together. Close this item here; track remaining work in that spec.

### 4. Favourites Management Surface — DEFERRED

Per human-owner direction (2026-04-21), de-prioritized. The favourites popup and any dedicated management surface are on the backlog but will not receive investment in the current roadmap. The underlying repository and command handlers remain supported; the UI polish is paused. See `SPEC_FEATURE_LOW_HANGING_FRUIT.md` → Deferred → D1 for the canonical statement.

Do not implement against this item without a priority change from the human owner.

### 5. UI Artifact Cleanup
Remove or integrate unused dialog/view-model artifacts that are not part of the active UI flow.

Targets:
- unused standalone dialog wrapper classes in `Presentation/Dialogs`
- unused view-models that are not wired into UI behavior

Acceptance:
- no dead UI classes remain without clear purpose
- docs match actual wired UI behavior

## Out of Scope
- advanced copy/move option design
