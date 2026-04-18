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
- show final result summary after completion/cancel/failure

Acceptance:
- progress appears during long operations (not only after completion)
- cancel request is routed and reflected in final summary

### 2. Centralized Shortcut Registry
Replace scattered keyboard shortcut handling with one reusable shortcut map/service.

Required behavior:
- one source of truth for command-to-shortcut mappings
- command bar tooltip text generated from the same source
- context routing preserved (dialogs, text input, pane list, shell)

Acceptance:
- shortcuts are defined in one place
- existing keyboard behavior remains unchanged

### 3. Persistence Enhancements
Extend persisted UI state beyond current pane path and inspector visibility/width.

Required behavior:
- persist and restore main window size and placement/state
- persist and restore per-pane sort column and direction
- persist and restore table column widths

Acceptance:
- restarting app restores these UI states reliably

### 4. Favourites Management Surface
Add a dedicated favourites management UI in addition to the quick flyout.

Required behavior:
- list favourites with display name and path
- remove and rename/edit display name
- open selected favourite in active pane

Acceptance:
- users can fully manage favourites without editing files/settings directly

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
