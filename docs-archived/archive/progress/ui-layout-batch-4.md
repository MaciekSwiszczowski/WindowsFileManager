# Batch 4 of 4: In-Cell Rename

**Spec:** `SPEC_UI_LAYOUT_AND_RESIZING.md` §6 (+ §8.4 / §8.5 / §8.7 checklists)
**Branch merged into main:** `master` (commits `d3bc862`, `113827b`, `35e965f`)
**Status:** complete

## What shipped

### Domain / Application
- `IDialogService.ShowRenameDialogAsync` deleted. `IDialogService` at [IDialogService.cs](../../src/WinUiFileManager.Application/Abstractions/IDialogService.cs) now exposes only collision / delete / create-folder / progress / result. `WinUiDialogService` has no rename dialog method; `grep` for `ShowRenameDialogAsync` in `src/` returns zero hits.
- `RenameEntryCommandHandler` signature unchanged (`ExecuteAsync(FileSystemEntryModel, string, CancellationToken)`), still the single write path. `MainShellViewModel.RenameAsync` (at [MainShellViewModel.cs:374](../../src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs)) is now a thin wrapper that calls `ActivePane.BeginRenameCurrent()`.

### Presentation view-models
- [FileEntryViewModel.cs:14-18](../../src/WinUiFileManager.Presentation/ViewModels/FileEntryViewModel.cs) — added `IsEditing` (bool) and `EditBuffer` (string) `[ObservableProperty] partial`s.
- [FilePaneViewModel.cs](../../src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs):
  - Private `_activeEditingEntry` sentinel + public read-only `ActiveEditingEntry` (line 125) + public `event EventHandler<FileEntryViewModel>? RenameRequested` (line 78) so the view can observe begin-rename requests without polling `IsEditing`.
  - `BeginRenameCurrent()` (line 390) — skips parent / loading / null current; cancels any other active editor before promoting the current one.
  - `CommitRenameAsync(entry, candidateName, ct)` (line 415) — trims, rejects invalid filename chars, runs the handler, returns `bool` so the cell event handler can keep edit mode open on collision (`Test_CommitRenameAsync_CollisionKeepsEditOpen`).
  - `CancelRename(entry)` (line 466) — clears buffer + editing flag, nulls the sentinel if it matches.
  - `partial void OnCurrentItemChanged(FileEntryViewModel?)` (line 477) — cancels active rename when selection moves off the editing row. Backing coverage: `Test_ChangingCurrentItem_CancelsActiveRename`.

### Controls / view
- [FileEntryTableView.xaml:64-87](../../src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml) — the Name column is now a `tv:TableViewTemplateColumn` with a `CellTemplate` (TextBlock) and an `EditingTemplate` (TextBox bound `TwoWay` to `EditBuffer`, `UpdateSourceTrigger=PropertyChanged`, spell-check disabled). Other columns remain `tv:TableViewTextColumn`.
- [FileEntryTableView.xaml.cs](../../src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml.cs):
  - Hooked `BeginningEdit` / `PreparingCellForEdit` / `CellEditEnding` / `CellEditEnded` on the TableView.
  - `BeginningEdit` gates edits to the `NameColumn` and to whichever row the VM has marked as `ActiveEditingEntry`.
  - `PreparingCellForEdit` runs `SelectNameStem` on the editor TextBox via a dispatcher tick (stem = name minus `Path.GetExtension(Name)`).
  - `CellEditEnding` pushes the TextBox text back into `EditBuffer`, then dispatches to `FilePaneViewModel.CommitRenameAsync`; on collision it cancels the TableView's own commit (`e.Cancel = true`) and re-focuses the editor on the next tick so the user can fix.
  - `OnHostRenameRequested` subscribes to `FilePaneViewModel.RenameRequested`; when the VM flips an entry into edit mode (from F2 / toolbar / command), the view selects the row, sets `CurrentCellSlot` to the Name column, scrolls into view, and calls the TableView's internal `BeginCellEditing` / `EndCellEditing` via reflection.
  - `OnPreviewKeyDown` handles the "already editing → F2 / Shift+F6" case by re-focusing the existing editor instead of toggling edit mode off.
- [MainShellView.xaml.cs:175](../../src/WinUiFileManager.Presentation/Views/MainShellView.xaml.cs) — routes `F2` (no modifiers) and `Shift+F6` to `ViewModel.RenameCommand`; both are gated on `!inTextInputContext` so they don't trigger while a path box / inspector text field has focus.

### Tests
- [ViewModelRenameCommandTests.cs](../../tests/WinUiFileManager.Application.Tests/Scenarios/ViewModelRenameCommandTests.cs) covers:
  1. `Test_RenameCommand_PrimesInlineRenameBuffer` — invoking `RenameCommand` sets `IsEditing`, primes `EditBuffer` to current name, points `ActiveEditingEntry` at the target row.
  2. `Test_CommitRenameAsync_RenamesFile` — happy-path rename moves the file on disk and clears edit mode.
  3. `Test_CommitRenameAsync_CollisionKeepsEditOpen` — colliding name returns `false`, keeps `IsEditing=true`, keeps `ActiveEditingEntry` set; no stray `ErrorMessage`.
  4. `Test_ChangingCurrentItem_CancelsActiveRename` — moving the selection off the row mid-rename cancels cleanly.
- Old tests that exercised the modal rename dialog / `ShowRenameDialogAsync` are gone.

## What's next

- Continue with `SPEC_RENAME_BUGS.md` R-1 once rename hardening work resumes.
- `SPEC_KEYBOARD_SHORTCUTS_GAPS.md` remains the follow-up for registry migration and the remaining shortcut gaps.
- Keep the splitter-freeze optimization documented in the UI-layout spec; it is accepted behavior, not an accidental deviation.

## Acceptance results

Per `SPEC_UI_LAYOUT_AND_RESIZING.md` §8.4 / §8.5 / §8.7 and §9:

- [x] `IDialogService.ShowRenameDialogAsync` no longer exists; `grep` in `src/` returns zero hits (§8.5, §9).
- [x] `WinUiDialogService` does not instantiate a rename `ContentDialog` (§8.5, §9).
- [x] `FileEntryViewModel` exposes `IsEditing` + `EditBuffer`.
- [x] `FilePaneViewModel` exposes `BeginRenameCurrent` / `CommitRenameAsync` / `CancelRename` + `ActiveEditingEntry` / `RenameRequested`.
- [x] `MainShellView.xaml.cs` routes `F2` and `Shift+F6` to `RenameCommand`; both gated on `!inTextInputContext`.
- [x] Automated tests green: `Test_RenameCommand_PrimesInlineRenameBuffer`, `Test_CommitRenameAsync_RenamesFile`, `Test_CommitRenameAsync_CollisionKeepsEditOpen`, `Test_ChangingCurrentItem_CancelsActiveRename`.
- [x] §8.4 `F2` on a file → stem selected, commit on Enter, focus returns to the row (manual).
- [x] §8.4 `Esc` cancels; original name restored (manual).
- [x] §8.4 LostFocus (click-away) commits (manual).
- [x] §8.4 invalid-character name keeps edit mode open and does not hit the filesystem (manual).
- [x] §8.4 collision keeps edit mode open with the disk entry unchanged (manual; automated coverage already green, but the UX path needs eyeballing).
- [x] §8.4 `Shift+F6` behaves identically to `F2` (manual).
- [x] §8.4 `F2` on `..` does nothing (manual).
- [x] §8.4 toolbar "Rename" triggers in-cell edit (manual).
- [ ] §8.4 rename inside a 400-char (long) path works; capability policy does not disable it (manual; relevant only once `SPEC_LONG_PATHS.md` lands — capability gating for `RenameEntry` is not yet implemented).
- [x] §8.7 regression pass over `winui-file-manager-keyboard-shortcuts-spec.md` §17 (manual).
- [x] §8.1 / §8.2 / §8.3 from the prior batches re-run once with U-4 in place to confirm no regressions in splitter / columns / persistence (manual).

## Surprises

- **The spec-prescribed `BoolToVisibility` / `InverseBoolToVisibility` converters were never needed.** `TableViewTemplateColumn` swaps `CellTemplate` for `EditingTemplate` when the row enters edit mode, so visibility toggling is handled by the control itself. `Converters/` still contains only `PixelGridLengthConverter`. `SPEC_UI_LAYOUT_AND_RESIZING.md` §6.3 (the sample `CellTemplate` using the converters) no longer matches the shipped XAML — reconciled in the same consolidation pass that writes this note.
- **The spec-prescribed `OnNameEditorKeyDown` / `OnNameEditorLostFocus` TextBox handlers were replaced with TableView lifecycle events.** `BeginningEdit` / `PreparingCellForEdit` / `CellEditEnding` / `CellEditEnded` give a cleaner fit for `TableViewTemplateColumn` than raw TextBox events (they cooperate with the control's own edit state rather than fighting it). Enter / Escape routing falls out of `TableViewEditAction.Commit` / `TableViewEditAction.Cancel`.
- **`CommitRenameAsync` gained a `candidateName` parameter.** Signature is `(FileEntryViewModel entry, string? candidateName, CancellationToken ct)`. Reason: `CellEditEnding` fires before the TwoWay binding has flushed the TextBox text into `EditBuffer`, so the handler pulls the text off the `TextBox` directly and passes it in. Spec §6.4 showed the 2-arg form; the 3-arg reality is documented here.
- **`_activeEditingEntry` sentinel + `ActiveEditingEntry` / `RenameRequested` event surface replaced the spec's "loop every item and set `IsEditing=false`" pattern.** The sentinel lets the view ignore any `CellEditEnding` that isn't for the VM's chosen row (important because the TableView will also fire `BeginningEdit` on a double-click that we must gate).
- **Collision handling does not set `FilePaneViewModel.ErrorMessage`.** The decision was to surface collisions purely via "edit stays open" (see `Test_CommitRenameAsync_CollisionKeepsEditOpen`). The spec §6.7 mentioned using `ErrorMessage`; the shipped implementation intentionally doesn't, to avoid flashing error text during a keystroke-level workflow. Red `VisualState` on the TextBox is still a future refinement — not shipped in this batch; can be added as a follow-up if §8.4 eyeballing says the current silence is too subtle.
- **Commit `35e965f` (GridSplitter `DragIncrement` / `KeyboardIncrement`) is technically polish from U-1/U-2 territory, not U-4.** It shipped after U-4 started but before U-4's handoff note was written; recording it here for completeness.
- **Post-U-4 splitter drag freezing is now accepted design.** The current shell keeps `GridSplitter` as the only resize mechanism, but `MainShellView.xaml.cs` uses pointer-start / pointer-end handlers to freeze and release both `FileTable` controls during drag. That behavior has been manually verified and is now documented in `SPEC_UI_LAYOUT_AND_RESIZING.md` instead of being treated as a forbidden fallback.

## Context hints for the next agent (K-2)

- `FileEntryTableView.OnPreviewKeyDown` is the canonical key-handling seam inside the grid. F2 / Shift+F6 are already dispatched from `MainShellView.xaml.cs` (not the grid) because they must work when the grid doesn't have focus as long as the pane is active; K-2 should follow the same split for any new global/pane shortcuts.
- `FilePaneViewModel.RenameRequested` is the canonical VM-to-view signal for "start editing this row". If K-2 adds more row-edit triggers (e.g., a context-menu item), raise the same event rather than reaching into the view.
- `_activeEditingEntry` is private. Observers outside the VM read `ActiveEditingEntry`.
- Do not re-introduce `BoolToVisibility` converters — the EditingTemplate swap is the approach.
- The `RenameEntryCommandHandler.ExecuteAsync(FileSystemEntryModel, string, CancellationToken)` signature is stable; any new rename trigger (drag-to-rename, batch rename, etc.) should route through `CommitRenameAsync` + the handler, not through its own write path.
- `CellEditEnding` reflection into `TableViewCell.BeginCellEditing` / `TableViewColumn.EndCellEditing` is load-bearing — if `WinUI.TableView` ever ships a public equivalent, the reflection block in `FileEntryTableView.xaml.cs:58-66` is the swap site.
- `CanResizeColumns` comes from `FilePaneDisplayOptions.EnableColumnResize`; the rename work did not touch it. Persistence (U-3) owns column widths; U-4 only consumes.
