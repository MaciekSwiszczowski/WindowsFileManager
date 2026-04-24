# Spec: FileEntryTableView

Scope: the pane list control only. This is the source of truth for `FileEntryTableView`, its vocabulary, its split header/body structure, its selection model, and the WinUI `TableView` workarounds this app depends on.

This spec consolidates behavior that was previously scattered across:
- `SPEC_UI_LAYOUT_AND_RESIZING.md`
- `SPEC_RENAME_BUGS.md`
- `BOOTSTRAP.md`
- `winui-file-manager-keyboard-shortcuts-spec.md`

## 1. Purpose

`FileEntryTableView` is the pane's file-list surface. It is responsible for:
- rendering the pane rows
- keeping keyboard navigation predictable
- owning row selection and multi-selection UI
- hosting in-place rename for the Name column
- mirroring sort and column-width state from the pane VM
- isolating the synthetic parent row `..` from normal child-item sorting and counting

The control is intentionally more prescriptive than a normal view spec because `WinUI.TableView` has several behaviors that are correct only if the control is wired in a very specific way.

## 2. Vocabulary

### 2.1 Active pane

The pane that currently receives file-manager commands. Exactly one pane is active.

### 2.2 Focus owner

The concrete WinUI control that currently has keyboard focus:
- `HeaderTable`
- `BodyTable`
- path box
- command button
- rename editor
- dialog input

### 2.3 Current item

The pane-local cursor target. This is `FilePaneViewModel.CurrentItem`.

It may be:
- the synthetic parent row `..`
- a folder entry
- a file entry

### 2.4 Explicit selection

The pane's actual multi-selection set, owned by the control and mirrored into `FilePaneViewModel.UpdateSelectionFromControl(...)`.

This is distinct from the current item.

### 2.5 Control selection

The visible `TableView.SelectedItem` / `SelectedItems` state used to render row highlight backgrounds.

The control may include the current body row in its visible selection even when the VM's explicit-selection set is empty.

### 2.6 Selection anchor

The body-table row that `WinUI.TableView` uses for Shift+Arrow / Shift+PageUp / Shift+PageDown range extension.

Because the control changes selection programmatically, this anchor must be re-synced manually.

### 2.7 Parent row / `..`

The synthetic row that navigates to the parent directory.

Rules:
- it exists only when the current directory is not a root folder
- it is represented by `FilePaneViewModel.ParentEntry`
- it is **not** part of `FilePaneViewModel.Items`
- it is excluded from explicit selection and command targets
- it never enters rename, sort, or item counting

### 2.8 Root folder

A directory with no parent row in the control. In the current app this means a drive root such as `C:\`.

### 2.9 Header table / body table

`FileEntryTableView` is a composite of two `WinUI.TableView` controls:

- `HeaderTable`
  - always visible
  - owns column headers
  - owns sort clicks and column resizing
  - hosts the synthetic parent row `..` when present
- `BodyTable`
  - hosts only real child entries from `FilePaneViewModel.Items`
  - owns extended row selection
  - owns rename editing

## 3. Structural model

The control must keep `ParentEntry` and `Items` separate.

Required invariants:
- `ParentEntry` lives outside the child-item list.
- `ItemCount` and status text count only real child items.
- sorting applies only to child items.
- `PaneColumnLayout` is captured from `HeaderTable` and mirrored to `BodyTable`.
- the header/body split is implementation, not just layout: keyboard routing must understand both tables.

## 4. Required behavior

### 4.1 Navigation and load handoff

On folder activation:
- the pane path updates immediately
- for non-root folders, `ParentEntry` is created immediately
- `CurrentItem` becomes `ParentEntry` immediately
- after load completes, the active pane restores focus to `HeaderTable`
- at a root folder, no parent row exists and focus lands in `BodyTable`

### 4.2 Sorting and column resizing

Only `HeaderTable` may:
- show headers
- change sort direction
- resize columns

`BodyTable` must mirror the captured widths and never resize independently.

### 4.3 Selection and focus visuals

The white focus frame must not be shown for either rows or cells during normal navigation.

Required mitigations:
- `UseSystemFocusVisuals="False"` on both tables
- transparent row-focus brushes in table resources
- clear `CurrentCellSlot` during ordinary row navigation
- use `FocusState.Programmatic` when returning focus to the tables after pane-level actions

The control still uses row selection backgrounds. The removal is about focus chrome, not selection highlighting.

### 4.4 Parent-row transitions

Keyboard transitions across the header/body boundary are part of the control contract:
- `Down` from `HeaderTable` moves to the first body row
- `End` from `HeaderTable` moves to the last body row
- `PageDown` from `HeaderTable` moves into the body by one page
- `Up` from the first body row may move to `ParentEntry`
- `Home` from `BodyTable` moves to `ParentEntry` when present
- `PageUp` from the first page of the body may move to `ParentEntry`

`Enter` on `ParentEntry` navigates up.

### 4.5 Multi-selection

`BodyTable` owns extended row selection.

Rules:
- `Ctrl+Click`, `Shift+Click`, `Ctrl+A`, `Ctrl+Shift+A`, `Insert`, `Space`, and `Ctrl+Space` apply to child rows only
- the parent row is excluded from explicit selection
- command targeting uses the pane VM's explicit-selection set first, then falls back to `CurrentItem`

### 4.6 Rename

Only child rows in `BodyTable` may enter rename.

Required rules:
- rename is wired only through the body Name column
- the parent row never enters edit mode
- `F2` and `Shift+F6` target the current child item only
- the editor selects the file stem, not the extension
- cell-edit lifecycle is routed through `BeginningEdit`, `PreparingCellForEdit`, `CellEditEnding`, and `CellEditEnded`

### 4.7 Loading overlay and focus retention

The pane must keep the list controls enabled while loading. Disabling the table causes focus to jump into shell chrome.

Required pattern:
- leave the table enabled
- block pointer interaction with a loading overlay
- restore focus to the active pane list when loading completes

### 4.8 Splitter-drag performance

During shell splitter drags, both tables in each pane may be frozen to `ActualWidth` and left-aligned, then restored to auto width on drag end.

This is an accepted performance mitigation and is part of the control contract.

## 5. WinUI.TableView findings

These points are implementation constraints, not optional observations.

### 5.1 Read-only cells swallow `DoubleTapped`

`TableViewCell` marks `DoubleTapped` handled when the cell is read-only. The control must subscribe with `AddHandler(..., handledEventsToo: true)` or folder activation becomes unreliable.

### 5.2 Cell focus and row selection are different systems

`CurrentCellSlot` can drift away from the visible row selection. If left alone, the app shows a white frame on a single cell while row selection backgrounds continue to work.

The control must treat cell focus as edit-mode-only state.

### 5.3 Keyboard-anchor state is internal

`WinUI.TableView` keeps range-selection anchor state in internal members such as:
- `LastSelectionUnit`
- `CurrentRowIndex`
- `SelectionStartRowIndex`
- `SelectionStartCellSlot`

Programmatic row changes must re-sync these values or Shift+selection starts from stale rows.

### 5.4 Disabling the table during load is wrong

If the table is disabled while a folder is loading, focus falls out to command buttons and the pane stops behaving like a keyboard-first file list.

### 5.5 Keeping `..` inside the child `ItemsSource` is wrong

Putting the parent row in the same `ItemsSource` as child entries makes all of these harder than they need to be:
- sorting
- explicit-selection filtering
- item counts
- "restore previous folder" behavior
- rename guards
- tests

The shipped direction is to keep the parent row separate in `HeaderTable`.

## 6. View-model contract

`FileEntryTableView` depends on these pane-VM semantics:
- `Items` contains only real child entries
- `ParentEntry` contains the synthetic `..` row or `null`
- `CurrentItem` may point to either `ParentEntry` or a child entry
- `UpdateSelectionFromControl(...)` receives child entries only
- `GetExplicitSelectedEntries()` excludes the parent row
- `ActiveEditingEntry`, `EditBuffer`, and `RenameRequested` are pane-level, not row-level

## 7. Cross-spec ownership

Detailed `FileEntryTableView` behavior now lives here.

Other specs should reference this document for:
- vocabulary
- parent-row semantics
- focus/selection expectations
- header/body table ownership
- `WinUI.TableView` workarounds

They should only keep the behavior that is specific to their own area, such as:
- shell splitter policy
- rename error UX
- global keyboard routing
- pane-level loading policy
