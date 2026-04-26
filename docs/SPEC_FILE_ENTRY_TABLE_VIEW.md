# Spec: SpecFileEntryTableView

`SpecFileEntryTableView` is a self-contained grid control that renders a single directory's contents in a Total Commander-style list: five fixed columns (Name, Ext, Size, Modified, Attributes), a synthetic `..` parent row pinned above the list, and a keyboard-first interaction model.

This document also specifies the messaging architecture the control participates in: the **keyboard manager**, the **file command coordinator**, and the **file operation dialog service**. Together these form a one-way data flow:

```
KeyboardManager ──primitive intents──▶ Coordinator ──resolved domain msgs──▶ Action services
                                          ▲
SpecFileEntryTableView ──state messages──────┘
```

External concerns not covered here: filesystem access, navigation history, persistence, the file operation service that performs rename / copy / move / delete, path normalization, clipboard adapters.

The target use case is a Windows Explorer replacement that behaves like Total Commander for folder navigation, the `..` entry, sorting, rename, selection, and filtering.

---

## 1. Data model

### 1.1 `FileEntryKind`

```
enum FileEntryKind { File, Folder }
```

There is no `Parent` value. The `..` row is synthetic and rendered by the control itself (§2.3); it is never represented by a `SpecFileEntryViewModel` instance.

### 1.2 `SpecFileEntryViewModel`

A row's view-model. The control reads these properties; it does not mutate instances.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | File or folder name. Displayed in the Name column. |
| `Extension` | `string` | File extension without leading dot, or empty string for extension-less items and folders. |
| `Size` | `string` | Formatted size (e.g. `"4.21 MB"`, `"512 B"`). Empty for folders. |
| `Modified` | `DateTime` | Last-write time in local time. Display formatting is owned by the Modified column. |
| `Attributes` | `string` | Formatted attribute flags. Empty if none. |
| `Kind` | `FileEntryKind` | Governs row styling and activation semantics. |

The control stores `Modified` as a `DateTime`; `Size` and `Attributes` remain display values.

---

## 2. Concepts

### 2.1 Cursor

The row the keyboard is "on". Exactly one cursor exists at any time. It may point to any real item or to the `..` row. Cursor is internal to the control.

### 2.2 Explicit selection

The set of real items the user has explicitly marked. This is internal control state and is announced with `FileTableSelectionChangedMessage`; it is not exposed as a dependency property. `..` is never present in the announced selection because `..` is not a real `SpecFileEntryViewModel`. However, `..` can be **visually** selected through user gestures — that highlight state is internal and not exposed externally.

### 2.3 Parent row (`..`)

A synthetic row the control renders at the top of the list when `IsRootContent == false`. Not a `SpecFileEntryViewModel`; not present in `ItemsSource`.

Rendering: Name shows `..`; Ext / Size / Modified / Attributes are blank; styled like a folder row.

Behavior:
- Cursor may land on it.
- Can be **visually** selected through a single click, `Ctrl+Click`, `Insert`, `Space`, `Ctrl+Space`, or a `Shift+` range that includes it.
- **Never appears in `SelectedItems`**.
- Activating it (Enter while cursor on `..`, or double-click on `..`) publishes `FileTableNavigateUpRequestedMessage` — only when `SelectedItems` is empty.
- Never enters rename. Never the target of any file operation. Never hidden by the filter. Always pinned above all real rows. Not affected by `SelectAll`.

When `IsRootContent == true`, the control omits the `..` row entirely.

### 2.4 Rename

The grid does not edit names. On a rename gesture, the **file operation dialog service** (§16) opens a popup; the grid is uninvolved beyond surfacing user state (selection, focus).

### 2.5 Filter

A string that hides real rows whose `Name` does not contain it (ordinal case-insensitive). Empty string disables the filter. The `..` row is unaffected. The host supplies its own input surface and binds it to `FilterText`.

### 2.6 Focused-for-keyboard

The `SpecFileEntryTableView` instance that most recently gained WinUI keyboard focus owns file-table keyboard input. Focus ownership is observable through `FileTableFocusedMessage`, not through a public dependency property.

When a table gains focus, it publishes `FileTableFocusedMessage(Identity)` and starts observing keyboard-manager messages. Every table also observes `FileTableFocusedMessage`; when a table receives the message with a foreign `Identity`, it stops observing keyboard-manager messages.

---

## 3. Public API

### 3.1 Dependency properties

| Property | Type | Mode | Default | Description |
|---|---|---|---|---|
| `ItemsSource` | `ObservableCollection<SpecFileEntryViewModel>` | OneWay | empty | Real items. Does not contain `..`. |
| `IsRootContent` | `bool` | OneWay | `false` | When `true`, the `..` row is not rendered. |
| `Identity` | `string` | OneWay | `""` | Tagged onto every outgoing message so consumers can distinguish table instances (e.g. `"Left"`, `"Right"`). |
| `FilterText` | `string` | OneWay | `""` | Substring filter. |
| `ColumnLayout` | `ColumnLayout` record | TwoWay | defaults per §4.1 | Column widths; updated on user drag. |

### 3.2 Supporting types

```
enum FileEntryColumn { Name, Extension, Size, Modified, Attributes }

record ColumnLayout(
    double NameWidth,
    double ExtensionWidth,
    double SizeWidth,
    double ModifiedWidth,
    double AttributesWidth);
```

### 3.3 Outgoing messages (control → messenger)

The control publishes exactly three message types via `IMessenger.Default.Send(...)`. All carry the `Identity` of the publishing instance.

```
record FileTableFocusedMessage(string Identity);

record FileTableSelectionChangedMessage(
    string Identity,
    IReadOnlyList<SpecFileEntryViewModel> SelectedItems,
    bool IsParentRowSelected);

record FileTableNavigateUpRequestedMessage(string Identity);
```

The control does **not** publish anything for activation of a real row (folder or file). Real-row activation flows through the keyboard manager's `ActivateInvokedMessage` (also published by the control itself on mouse double-click of a real row); the coordinator resolves it from the active table's selection.

Trigger conditions are specified in §13.

### 3.4 Incoming messages (messenger → control)

The focused control subscribes to the primitive input messages emitted by the keyboard manager (§12). Non-focused controls do not observe those messages.

The control does not expose any custom CLR events.

---

## 4. Columns

### 4.1 Column specification

| # | Column | Bound to | Default width | Min width | Alignment |
|---|---|---|---:|---:|---|
| 1 | Name | `Name` | 320 | 100 | Left, character ellipsis |
| 2 | Ext | `Extension` | 40 | 32 | Left, character ellipsis, muted |
| 3 | Size | `Size` | 70 | 56 | Right, character ellipsis |
| 4 | Modified | `Modified` | 120 | 100 | Left, `TableViewDateColumn` formatting |
| 5 | Attr | `Attributes` | 50 | 40 | Left, character ellipsis, muted |

Reordering, per-column filtering, and show/hide are not supported.

### 4.2 Header

Always visible. Column captions and sort indicators. Resizable via right-edge drag — `ColumnLayout` updates live; widths clamp at the column's minimum.

### 4.3 Row

Row height, selection highlight, and other visual sizing take the control's defaults. The application merges the WinUI **compact sizes** resource dictionary at app level, so rows and headers inherit the denser compact metrics out of the box. Fine-tuning is deferred.

The control virtualizes rows. A directory of 100 000 items must scroll smoothly.

No focus rectangle is drawn on cells or rows during keyboard navigation. Only the selection background marks the cursor.

### 4.4 Empty cells

Folder rows have an empty Size cell. The `..` row has empty Ext / Size / Modified / Attributes cells.

---

## 5. Sorting

Exactly one column header shows the sort indicator. Sort state is internal to the control.

Clicking a header:
- Already the sort column → toggle the internal sort direction.
- Otherwise → set the internal sort column to the clicked column and use ascending direction.

The control does **not** expose sort state as dependency properties. Persisted UI state is limited to `ColumnLayout`.

`..` is always pinned visually first regardless of sort state. Directory-before-file ordering is a host-side decision.

---

## 6. Navigation and activation

User-visible behavior. Inputs arrive as keyboard-manager messages (§12); mouse interactions are handled in-control (§9).

### 6.1 Cursor movement

| Behavior | Effect |
|---|---|
| Move cursor up | Cursor up one row. From the first real row with `..` shown, lands on `..`. At top with no `..`: no-op. |
| Move cursor down | Cursor down one row. At last visible row: no-op. |
| Page up / down | One visible page; clamps at boundary; crosses to `..` at the seam. |
| Home | Cursor to `..` if shown, else first visible real row. |
| End | Cursor to last visible real row, or `..` if list is empty. |

All cursor movements scroll the target into view.

### 6.2 Activation

There are two activation paths, distinguished by what's under the cursor or click target.

**Navigate-up path.** The control publishes `FileTableNavigateUpRequestedMessage` when:

> `SelectedItems` is empty **and** (the user pressed `Enter` while focused with the cursor on `..`, or double-clicked the `..` row).

If `SelectedItems` is non-empty, the user is presumed to have a marked set ready for some other command, and the navigate-up message is not published. The user clears the selection (`Esc`) before navigating up via `Enter` on `..`.

**Default-action path.** The control does **not** publish anything for activation of a real row. The keyboard manager publishes `ActivateInvokedMessage` on `Enter`. The control itself republishes the same `ActivateInvokedMessage` on mouse double-click of a real row (a single click on a row already places that row into `SelectedItems` per §7.5, so the second click finds the table in exactly the state the coordinator's resolution rule expects). The coordinator subscribes to `ActivateInvokedMessage` and resolves it against the active table's selection (§14).

### 6.3 Navigate up

`Backspace` / `Ctrl+PageUp` / `Alt+Up` are interpreted by the keyboard manager as primitive navigate-up intents and resolved by the coordinator. The control is uninvolved (it has no parent path). Activating `..` (above) is the only navigate-up path that involves the table.

---

## 7. Selection

### 7.1 Independence

Cursor-movement messages never change selection. Toggle messages never move the cursor. Range messages do both.

### 7.2 Toggle semantics

| Behavior | Effect |
|---|---|
| Toggle at cursor | On a real row, toggle in `SelectedItems`. On `..`, toggle visual highlight only and publish `FileTableSelectionChangedMessage` with updated `IsParentRowSelected`. Cursor does not move. |
| Toggle at cursor and advance | Same toggle, then move cursor down by one row. At last row: toggle, do not move. On `..`: toggle visual, advance to first real row. |

### 7.3 Bulk selection

| Behavior | Effect |
|---|---|
| Select all | Add every visible real row to `SelectedItems`. `..`'s visual state is untouched. Cursor preserved (or lands on first real row if previously `null`). |
| Clear selection | Empty `SelectedItems` and clear `..`'s visual highlight. Cursor preserved. |

### 7.4 Range extension

Range extension uses the cursor as anchor; programmatic cursor moves keep the anchor fresh.

| Behavior | Effect |
|---|---|
| Extend up / down | Cursor moves one row, the new cursor row joins the range. If new cursor is `..`, `..` is visually highlighted but not in `SelectedItems`. |
| Extend page up / down | Cursor moves one page; all crossed rows joined to the range. |
| Extend to first / last | Cursor moves to `..` (or first real row if `..` hidden) / last real row; all crossed rows joined. |

### 7.5 Mouse selection

| Gesture | Effect |
|---|---|
| Click on a real row | Clear body-table `SelectedItems`, **add the clicked row to `SelectedItems`**, move cursor there. If `..` was already visually selected, keep it visually selected; otherwise leave it unselected. |
| Click on `..` | Clear `SelectedItems`, visually select `..`, move cursor to `..`. (`..` is never added to `SelectedItems`.) |
| `Ctrl+Click` on real row | Toggle that row in `SelectedItems`; cursor moves there. |
| `Ctrl+Click` on `..` | Toggle `..`'s visual highlight; cursor moves to `..`. |
| `Shift+Click` | Range select from previous cursor to clicked row; cursor moves there. |
| Double-click on a real row | The two clicks fire as singles first, leaving `SelectedItems = [clickedRow]`. The double-click gesture then publishes `ActivateInvokedMessage`; the coordinator resolves it via the selection (§14). |
| Double-click on `..` | The two clicks fire as singles first, leaving `SelectedItems` empty and `..` visually selected. The double-click gesture then publishes `FileTableNavigateUpRequestedMessage`. |

Any pointer interaction brings WinUI keyboard focus to the control. The focus transition publishes `FileTableFocusedMessage` and activates that table's keyboard-message subscriptions.

---

## 8. Filter

When `FilterText` is non-empty, a real row is visible iff `Name.Contains(filter, OrdinalIgnoreCase)`. Empty string shows everything. `..` is always visible.

Effects:
- **List**: rendering only; `ItemsSource` is not mutated.
- **Cursor**: if the cursor row is hidden, cursor moves to the first visible real row, or to `..` if no real row is visible, or `null` at root with empty result.
- **Selection**: `SelectedItems ⊆ currently-visible real rows` at all times. When a row becomes hidden, it is pruned from `SelectedItems` and a `FileTableSelectionChangedMessage` is published. Clearing the filter does **not** restore previously-pruned rows.
- **Navigation**: all keyboard movement skips hidden rows.

Sort and column widths are preserved across filter changes.

---

## 9. Focus

The control does not expose focus state. Focus is represented by message subscriptions:

- On `GotFocus`, the table publishes `FileTableFocusedMessage(Identity)` and starts observing keyboard-manager messages.
- Every table remains subscribed to `FileTableFocusedMessage`.
- When a table receives `FileTableFocusedMessage` with a foreign `Identity`, it stops observing keyboard-manager messages.
- On `LostFocus`, the table stops observing keyboard-manager messages. This covers focus moving to non-table controls such as logger, path box, buttons, or dialogs.

Keyboard-message handlers do not need their own focus guards because inactive tables are not subscribed to those messages.

The control does not disable itself for any reason. Hosts that block interaction during loads use a hit-testable overlay rather than disabling the control.

No system focus rectangle is drawn on rows or cells.

Every `GotFocus` activation publishes `FileTableFocusedMessage(Identity)`. Loss of focus is implicit for the host; the next gain identifies the new owner.

---

## 10. Visual details

- Dark and light themes both render the accent selection at readable contrast.
- Header uses chrome-medium-low background.
- Ext and Attributes cells use the medium-base foreground (muted).
- Name and Modified cells use the default foreground.
- All text cells ellipsise; no wrapping.
- Vertical scroll bar only when overflow; never horizontal.
- `..` is styled identically to a folder row.
- When `..` is visually selected, it renders the standard selection background.

---

## 11. Total Commander parity

- `..` always pinned at the top.
- Click sorts; click again reverses.
- Navigate-up activation publishes `FileTableNavigateUpRequestedMessage`; real-row activation flows through `ActivateInvokedMessage` and is resolved by the coordinator into navigate-into / open-file.
- `Insert` toggles + advances; `Space` / `Ctrl+Space` toggle without advancing.
- `Backspace` / `Ctrl+PageUp` / `Alt+Up` request navigate-up.
- `F2` / `Shift+F6` request rename of the single selected item.
- `F5` / `F6` / `F8` operate on `SelectedItems`; empty selection is a no-op.
- `Ctrl+A` / `Ctrl+Shift+A` select all / clear.
- `..` can be visually marked but never leaks into command targets.

Out of scope:
- Back / forward history; file-type icons; folder tabs; grouping; thumbnails; preview; locale-aware collation; drag-and-drop.

---

## 12. Keyboard input

The shell attaches `KeyboardInputBehavior.Command` to the element that should own file-manager keyboard routing. The behavior listens to that element's `PreviewKeyDown`, converts the key and modifier state into `KeyboardInput`, invokes the bound command, and applies `KeyboardInput.Handled` back to the event.

`KeyboardManager` exposes the command consumed by the behavior. It has no reference to a root `UIElement`. It translates recognized keystrokes to one **primitive intent message** through the messenger and marks the input handled. Unrecognized keys are ignored.

The keyboard manager is dumb on purpose: it does not know which table is focused, what is selected, or what commands mean. It just translates keystrokes to messages.

### 12.1 Primitive intent messages

State-mutation messages (consumed only by the `SpecFileEntryTableView` currently observing keyboard-manager messages):

| Message | Default shortcut | Behavior in the focused table |
|---|---|---|
| `MoveCursorUpMessage` | `Up` | §6.1 |
| `MoveCursorDownMessage` | `Down` | §6.1 |
| `MoveCursorPageUpMessage` | `PageUp` | §6.1 |
| `MoveCursorPageDownMessage` | `PageDown` | §6.1 |
| `MoveCursorHomeMessage` | `Home`, `Ctrl+Home` | §6.1 |
| `MoveCursorEndMessage` | `End`, `Ctrl+End` | §6.1 |
| `ExtendSelectionUpMessage` | `Shift+Up` | §7.4 |
| `ExtendSelectionDownMessage` | `Shift+Down` | §7.4 |
| `ExtendSelectionPageUpMessage` | `Shift+PageUp` | §7.4 |
| `ExtendSelectionPageDownMessage` | `Shift+PageDown` | §7.4 |
| `ExtendSelectionHomeMessage` | `Shift+Home` | §7.4 |
| `ExtendSelectionEndMessage` | `Shift+End` | §7.4 |
| `ToggleSelectionAtCursorMessage` | `Space`, `Ctrl+Space` | §7.2 |
| `ToggleSelectionAtCursorAndAdvanceMessage` | `Insert` | §7.2 |
| `SelectAllMessage` | `Ctrl+A` | §7.3 |
| `ClearSelectionMessage` | `Ctrl+Shift+A`, `Esc` (when selection non-empty) | §7.3 |
| `ActivateInvokedMessage` | `Enter` (also published by the file table on mouse double-click of a real row) | If `SelectedItems` is empty AND the cursor is on `..`, publish `FileTableNavigateUpRequestedMessage` (§6.2). Otherwise the file table takes no further action; the coordinator handles activation against the active selection. |

Command intent messages (consumed by the **coordinator**, §14):

| Message | Default shortcut |
|---|---|
| `NavigateUpKeyPressedMessage` | `Backspace`, `Ctrl+PageUp`, `Alt+Up` |
| `RenameKeyPressedMessage` | `F2`, `Shift+F6` |
| `DeleteKeyPressedMessage` | `Delete`, `F8`, `Shift+Delete` |
| `CopyKeyPressedMessage` | `F5` |
| `MoveKeyPressedMessage` | `F6` |
| `CreateFolderKeyPressedMessage` | `F7`, `Ctrl+Shift+N` |
| `CopyPathKeyPressedMessage` | `Ctrl+Shift+C` |
| `PropertiesKeyPressedMessage` | `Alt+Enter` |

`Esc` is routed by the keyboard manager: if a dialog is open it goes there; if a filter input has focus it clears the filter; if any focused table has non-empty `SelectedItems` it publishes `ClearSelectionMessage`; otherwise it propagates.

### 12.2 Out of scope for the file-table input path

`Tab` / `Shift+Tab` (pane switch — drives WinUI focus directly), `Ctrl+L` (focus path box), `Ctrl+I` (inspector toggle), `Ctrl+R` (refresh — reloads `ItemsSource` externally), `Ctrl+D` (favourites), `Alt+Left` / `Alt+Right` (navigation history), printable characters into the host's filter input.

---

## 13. File-table outgoing messages — trigger rules

The control publishes exactly three message types.

### 13.1 `FileTableFocusedMessage(Identity)`

Fires when the table gains WinUI keyboard focus. Loss is implicit.

### 13.2 `FileTableSelectionChangedMessage(Identity, SelectedItems, IsParentRowSelected)`

Fires whenever `SelectedItems` changes or the `..` row's visual selection changes. `SelectedItems` is a snapshot in `ItemsSource` order and never contains `..`; `IsParentRowSelected` reports whether `..` is visually selected. Redundant publications are suppressed only when both the selected-item snapshot and `IsParentRowSelected` match the previous publication.

Trigger sources:
- Toggle / extend / select-all / clear-selection messages affecting real rows.
- `..` visual selection / deselection, even when `SelectedItems` is unchanged.
- Mouse `Ctrl+Click` / `Shift+Click`.
- A click that clears a previously-non-empty `SelectedItems`.
- `FilterText` change that prunes selected rows.

### 13.3 `FileTableNavigateUpRequestedMessage(Identity)`

Fires when:
- `SelectedItems` is empty, **and**
- One of:
  - The user pressed `Enter` (delivered as `ActivateInvokedMessage` while the table is observing keyboard-manager messages) with the cursor on `..`.
  - The user double-clicked the `..` row.

Does not fire when `SelectedItems` is non-empty. Does not fire when the cursor is on a real row (real-row activation flows through `ActivateInvokedMessage`, resolved by the coordinator).

### 13.4 `ActivateInvokedMessage` (republished from mouse)

The file table re-publishes `ActivateInvokedMessage` on mouse double-click of a real row. The keyboard manager publishes the same message on `Enter`. Both feed the coordinator's single resolution path (§14.5). The file table does not republish on double-click of `..` — that path produces `FileTableNavigateUpRequestedMessage` instead.

---

## 14. Command resolution

### 14.1 Role

A single application-scoped service that holds two pieces of cross-cutting state and bridges keyboard primitives to resolved domain commands. The coordinator is the **only** place the rule "no selection → no command" is enforced.

### 14.2 State held

```
string? activeIdentity;
Dictionary<string, IReadOnlyList<SpecFileEntryViewModel>> selectionByIdentity;
```

- `_activeIdentity` — last-focused table; never reset to null (focus may move to non-table chrome, but the "active" pane stays).
- `_selectionByIdentity` — current `SelectedItems` per table.

The coordinator does **not** track cursor. The two activation paths handle their cursor needs locally — the file table inspects its own cursor before publishing `FileTableNavigateUpRequestedMessage`, and real-row activation places the target into `SelectedItems` (via the click portion of a double-click, or via prior selection) before the coordinator sees `ActivateInvokedMessage`.

### 14.3 Subscriptions

From the file table:
- `FileTableFocusedMessage` → set `_activeIdentity = msg.Identity`.
- `FileTableSelectionChangedMessage` → `_selectionByIdentity[msg.Identity] = msg.SelectedItems`.
- `FileTableNavigateUpRequestedMessage` → §14.5.

From the keyboard manager (or, for `ActivateInvokedMessage`, also from the file table on mouse double-click):
- `ActivateInvokedMessage` → §14.5.
- `NavigateUpKeyPressedMessage` → §14.5.
- `RenameKeyPressedMessage`, `DeleteKeyPressedMessage`, `CopyKeyPressedMessage`, `MoveKeyPressedMessage`, `CreateFolderKeyPressedMessage`, `CopyPathKeyPressedMessage`, `PropertiesKeyPressedMessage` → §14.5.

### 14.4 Resolved domain messages

Published by the coordinator for the action services to consume. Each carries the `SourceIdentity` (the table the command is acting on).

```
record NavigateUpRequestedMessage(string SourceIdentity);

record DefaultActionRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);

record RenameRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);

record DeleteRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);

record CopyRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);

record MoveRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);

record CreateFolderRequestedMessage(string SourceIdentity);

record CopyPathRequestedMessage(
    string SourceIdentity,
    IReadOnlyList<SpecFileEntryViewModel> Items);

record PropertiesRequestedMessage(
    string SourceIdentity,
    SpecFileEntryViewModel Item);
```

### 14.5 Resolution rules

In each rule, "active selection" means `_selectionByIdentity[_activeIdentity]` (empty if `_activeIdentity` is null or has no entry).

| Trigger | Resolution |
|---|---|
| `FileTableNavigateUpRequestedMessage` | Publish `NavigateUpRequestedMessage(msg.Identity)`. |
| `ActivateInvokedMessage` | Active selection has exactly one item → publish `DefaultActionRequestedMessage(_activeIdentity, items[0])`. Zero or multiple → no-op. |
| `NavigateUpKeyPressedMessage` | If `_activeIdentity` is null → no-op. Otherwise publish `NavigateUpRequestedMessage(_activeIdentity)`. |
| `RenameKeyPressedMessage` | If active selection has exactly one item → publish `RenameRequestedMessage(_activeIdentity, items[0])`. Zero or multiple → no-op. |
| `DeleteKeyPressedMessage` | Active selection empty → no-op. Otherwise publish `DeleteRequestedMessage(_activeIdentity, items)`. |
| `CopyKeyPressedMessage` | Active selection empty → no-op. Otherwise publish `CopyRequestedMessage(_activeIdentity, items)`. |
| `MoveKeyPressedMessage` | Active selection empty → no-op. Otherwise publish `MoveRequestedMessage(_activeIdentity, items)`. |
| `CreateFolderKeyPressedMessage` | If `_activeIdentity` is null → no-op. Otherwise publish `CreateFolderRequestedMessage(_activeIdentity)`. |
| `CopyPathKeyPressedMessage` | Active selection empty → no-op. Otherwise publish `CopyPathRequestedMessage(_activeIdentity, items)`. |
| `PropertiesKeyPressedMessage` | If active selection has exactly one item → publish `PropertiesRequestedMessage(_activeIdentity, items[0])`. Zero or multiple → no-op. |

The coordinator never publishes a domain message with empty `Items`. The "no selection → nothing happens" rule lives entirely in this table.

### 14.6 Why the coordinator does not track the cursor

Two cases that might seem to need cursor knowledge are handled by the file table directly:

- **Navigate up via `..` activation** — the file table inspects its own cursor and `SelectedItems`, then publishes `FileTableNavigateUpRequestedMessage` when conditions match (§13.3).
- **Default action on a real row** — by the time `ActivateInvokedMessage` arrives, a single click (or the click portion of a double-click) has already placed the activation target into `SelectedItems`. The coordinator's "exactly one item" rule resolves it.

Every other command (Rename, Copy, Move, Delete, CopyPath, Properties, CreateFolder, NavigateUp by keystroke) operates on selection or on the active pane as a whole — selection is enough.

---

## 15. Action services

Each domain message has at least one consumer. The dialog service (§16) handles the ones that need a popup; other consumers handle the rest.

| Message | Primary consumer | Notes |
|---|---|---|
| `NavigateUpRequestedMessage` | Navigation host | Loads the parent directory if one exists; no-op at root. |
| `DefaultActionRequestedMessage` | Navigation host | Folder → navigate into; file → open with default Windows app. |
| `RenameRequestedMessage` | File operation dialog service | §16.2 |
| `DeleteRequestedMessage` | File operation dialog service | §16.3 |
| `CopyRequestedMessage` | File operation dialog service | §16.4 |
| `MoveRequestedMessage` | File operation dialog service | §16.5 |
| `CreateFolderRequestedMessage` | File operation dialog service | §16.6 |
| `CopyPathRequestedMessage` | Clipboard adapter | Joins paths, writes to clipboard. No dialog. |
| `PropertiesRequestedMessage` | Shell integrator | Invokes the native Windows Properties dialog. |
| `FileTableSelectionChangedMessage` | Status-bar presenter, inspector pane | Update "N selected (M bytes)", show item details, etc. |

Other consumers may subscribe (e.g. logging, telemetry).

---

## 16. File operation dialog service

### 16.1 Overview

`FileOperationDialogService` is an application-scoped singleton that subscribes to the dialog-driven domain messages, opens the appropriate dialog, and on confirmation delegates to a file-operation service (abstracted out of this spec).

Dependencies:
- `IMessenger`.
- A reference to the main window or its `XamlRoot` for dialog parenting.
- An abstract file-operation service for the actual rename / copy / move / delete / create.
- An abstract destination-path provider — given a source `Identity`, returns the destination pane's current path (for copy / move) or the source pane's own path (for create-folder).

Concurrency: at most one dialog open at a time. Subsequent messages queue and process FIFO.

Cancellation: every dialog has a Cancel button. Cancel closes silently, no operation.

Errors: failures from the file-operation service surface via a lightweight non-modal indicator on the source pane (details out of scope).

### 16.2 `RenameRequestedMessage` — rename popup

1. Open a `ContentDialog` titled "Rename".
2. Single text field pre-populated with `Item.Name`. The file stem (text before the last `.`) is pre-selected. Dotfiles select the whole name.
3. Buttons: **Rename** (primary, `Enter`), **Cancel** (secondary, `Esc`).
4. **Rename**:
   - Trim input. Empty or unchanged → close, no operation.
   - Invalid filename characters (`\ / : * ? " < > |`) → inline validation, dialog stays open.
   - Otherwise → invoke rename. On success → close. On failure (collision, permission, source-gone, path-too-long) → inline error message, dialog stays open, typed name preserved.

### 16.3 `DeleteRequestedMessage` — delete confirmation

1. Open a `ContentDialog` titled "Delete".
2. Body: `"Permanently delete N item(s)? They will NOT go to the Recycle Bin."` plus a truncated preview of item names.
3. Buttons: **Delete** (primary), **Cancel** (secondary).
4. **Delete** → invoke delete on the whole batch. Errors surface per §16.1.

Always confirms; there is no skip-confirm shortcut.

### 16.4 `CopyRequestedMessage` — copy destination

1. Resolve the destination path via the destination-path provider, passing `SourceIdentity`.
2. Open a `ContentDialog` titled "Copy".
3. Body: source summary (`"Copy N item(s)"` + name preview) + editable destination path textbox pre-filled with the resolved destination.
4. Buttons: **Copy** (primary), **Cancel** (secondary).
5. **Copy**: validate destination (non-empty, plausible path) → invoke copy. Progress and conflict UX out of scope.

### 16.5 `MoveRequestedMessage` — move destination

Identical to §16.4 but titled "Move", invokes move, and expects the source pane's selection to be cleared on success (the host's responsibility once `ItemsSource` updates).

### 16.6 `CreateFolderRequestedMessage` — create folder

1. Resolve the source pane's current path via the destination-path provider.
2. Open a `ContentDialog` titled "Create Folder".
3. Single text field, placeholder "Folder name".
4. Buttons: **Create** (primary), **Cancel** (secondary).
5. **Create**: validate empty / invalid characters / collision inline → invoke create. On success the host is expected to surface the new folder as the cursor and sole selection (after `ItemsSource` updates).

### 16.7 Messages the dialog service does not handle

`FileTableSelectionChangedMessage`, `NavigateUpRequestedMessage`, `DefaultActionRequestedMessage`, `CopyPathRequestedMessage`, `PropertiesRequestedMessage`, `FileTableFocusedMessage`, `FileTableNavigateUpRequestedMessage` — see §15 for owners.

---

## 17. Manual verification checklist

### 17.1 Rendering

- [ ] Five columns appear in order Name, Ext, Size, Modified, Attributes; default widths and minimums match §4.1.
- [ ] Rows and headers render at compact-density sizing.
- [ ] Name and Modified cells use default foreground; Ext and Attributes are muted; Size is right-aligned.
- [ ] Long text ellipsises; nothing wraps.
- [ ] No horizontal scroll bar; vertical only on overflow.
- [ ] No focus rectangle during keyboard navigation.

### 17.2 Data binding

- [ ] `ItemsSource` add / remove / re-order propagates to visible rows.
- [ ] `IsRootContent` toggle shows / hides `..`.
- [ ] Setting `ColumnLayout`, `FilterText`, and `Identity` propagates as expected.

### 17.3 Focus

- [ ] Clicking any row or tabbing in publishes `FileTableFocusedMessage`.
- [ ] Clicking any row or tabbing in starts keyboard-manager message observation for that table.
- [ ] A foreign `FileTableFocusedMessage` stops keyboard-manager message observation.
- [ ] Focus to a non-table control stops keyboard-manager message observation; no message is published on loss.
- [ ] In a multi-table host, only the focused table reacts to keyboard-manager input messages.
- [ ] Non-focused instances are not subscribed to keyboard-manager input messages.

### 17.4 Parent row

- [ ] With `IsRootContent = false`, `..` renders above all real rows, styled as a folder.
- [ ] With `IsRootContent = true`, `..` is absent and its navigation rules have no effect.
- [ ] Sort never pushes `..` below a real row.
- [ ] `..` visible under any `FilterText`.
- [ ] `..` never present in `SelectedItems`.
- [ ] `..` never appears in `FileTableSelectionChangedMessage.SelectedItems`; its visual selection is reported only by `IsParentRowSelected`.
- [ ] `..`'s visual selection toggles via `Ctrl+Click`, `Insert`, `Space`, `Ctrl+Space`, `Shift+` range.
- [ ] Activating `..` (Enter while cursor on `..` and `SelectedItems` empty, or double-click on `..`) publishes `FileTableNavigateUpRequestedMessage`.
- [ ] When `SelectedItems` is non-empty, neither Enter on `..` nor double-click on `..` publishes the navigate-up message.
- [ ] `..` never targeted by Rename / Delete / Copy / Move / CopyPath / Properties.

### 17.5 Cursor movement

- [ ] `MoveCursorUpMessage` on `..`: no-op. From first real row with `..`: lands on `..`.
- [ ] `MoveCursorDownMessage` at last: no-op. From `..`: first real row.
- [ ] `MoveCursorPageUp` / `MoveCursorPageDown` clamp; cross the seam.
- [ ] `MoveCursorHome` lands on `..` if shown, else first real row.
- [ ] `MoveCursorEnd` lands on last real row, or `..` on empty list.
- [ ] All cursor changes scroll the target into view.
- [ ] Non-focused tables ignore every `MoveCursor*Message`.

### 17.6 Selection

- [ ] `ToggleSelectionAtCursorMessage` on real row: toggle, publish `FileTableSelectionChangedMessage`. On `..`: visual only, publish with updated `IsParentRowSelected`.
- [ ] `ToggleSelectionAtCursorAndAdvanceMessage`: toggle then advance. At last row: no advance. On `..`: visual + advance to first real row.
- [ ] `SelectAllMessage` adds every visible real row, publishes once; `..`'s visual state untouched.
- [ ] `ClearSelectionMessage` empties `SelectedItems` + clears `..`'s visual; publishes only if `SelectedItems` was non-empty.
- [ ] `ExtendSelection*Message` boundary clamp; new cursor row enters the range; `..` joins visually if crossed but stays out of `SelectedItems`.
- [ ] After a programmatic cursor move, the next range op anchors from the new cursor.

### 17.7 Mouse

- [ ] Click on a real row after `..` is visually selected → `..` remains visually selected, clicked row is selected, and `FileTableSelectionChangedMessage` reports the clicked real row only.
- [ ] Click on a real row when `..` is not visually selected → `SelectedItems` cleared and the clicked row added to it; cursor moves there.
- [ ] Click on `..` → `SelectedItems` cleared, `..` visually selected, cursor moves to `..` (`..` not added to `SelectedItems`).
- [ ] `Ctrl+Click` on real row → toggle in `SelectedItems`; cursor moves there.
- [ ] `Ctrl+Click` on `..` → toggle `..`'s visual highlight; cursor moves to `..`.
- [ ] `Shift+Click` → range from previous cursor to clicked row; cursor moves.
- [ ] Double-click on a real row → after the click puts the row into `SelectedItems`, the double-click publishes `ActivateInvokedMessage`; coordinator publishes `DefaultActionRequestedMessage(clickedRow)`.
- [ ] Double-click on `..` → after the click clears `SelectedItems` and visually marks `..`, the double-click publishes `FileTableNavigateUpRequestedMessage`; coordinator publishes `NavigateUpRequestedMessage`.
- [ ] Click on header sorts; drag header right edge resizes a column.

### 17.8 Activation

- [ ] `ActivateInvokedMessage` while focused, `SelectedItems` empty, cursor on `..` → publishes `FileTableNavigateUpRequestedMessage`.
- [ ] `ActivateInvokedMessage` while focused, `SelectedItems` empty, cursor on real row → no `FileTableNavigateUpRequestedMessage` (the file table is silent); coordinator's resolution rule sees an empty selection and emits no domain message.
- [ ] `ActivateInvokedMessage` while focused, `SelectedItems` has exactly one item → coordinator publishes `DefaultActionRequestedMessage` with that item; the file table publishes nothing extra.
- [ ] `ActivateInvokedMessage` while focused, `SelectedItems` has multiple items → no domain message published.
- [ ] `ActivateInvokedMessage` while the table is not observing keyboard-manager messages → ignored entirely.
- [ ] Mouse double-click reproduces all of the above conditions because the click-portion has updated `SelectedItems` first.

### 17.9 Filter

- [ ] `FilterText = "abc"` hides non-matching real rows; `..` still visible.
- [ ] Cursor on hidden row moves to first visible real row.
- [ ] Hidden rows pruned from `SelectedItems`; publishes a `FileTableSelectionChangedMessage`.
- [ ] `SelectedItems ⊆ visible real rows` invariant holds at all times.
- [ ] Clearing filter does not restore previously-pruned rows.
- [ ] Sort and column widths preserved.

### 17.10 Sort and columns

- [ ] Header click sets / toggles the internal sort indicator.
- [ ] The control does not re-order `ItemsSource`.
- [ ] Column drag updates `ColumnLayout`; clamps at minimum.
- [ ] Setting `ColumnLayout` programmatically applies to all five columns.

### 17.11 Coordinator

- [ ] `FileTableFocusedMessage` updates `_activeIdentity`.
- [ ] `FileTableSelectionChangedMessage` updates the per-identity selection map.
- [ ] `NavigateUpKeyPressedMessage` publishes `NavigateUpRequestedMessage` with the active identity.
- [ ] `CopyKeyPressedMessage` with empty active selection → no `CopyRequestedMessage`.
- [ ] `CopyKeyPressedMessage` with non-empty active selection → publishes `CopyRequestedMessage` with those items.
- [ ] Same rule applies for Move, Delete, CopyPath.
- [ ] `RenameKeyPressedMessage` with active selection of size ≠ 1 → no `RenameRequestedMessage`.
- [ ] `RenameKeyPressedMessage` with active selection of size 1 → `RenameRequestedMessage` with that item.
- [ ] `PropertiesKeyPressedMessage` follows the same size-1 rule.
- [ ] `CreateFolderKeyPressedMessage` always publishes `CreateFolderRequestedMessage` with the active identity.
- [ ] `FileTableNavigateUpRequestedMessage` publishes `NavigateUpRequestedMessage` with the same `Identity`.
- [ ] `ActivateInvokedMessage` with active selection of size 1 publishes `DefaultActionRequestedMessage(items[0])`. Size 0 or 2+ → no domain message.

### 17.12 File operation dialog service

- [ ] `RenameRequestedMessage` opens the rename popup with the name pre-filled and stem pre-selected; dotfiles select whole name.
- [ ] Empty / unchanged input closes silently; invalid characters / collision / permission / source-gone keep dialog open with typed name preserved.
- [ ] `DeleteRequestedMessage` shows confirmation with item count and preview; Delete commits; Cancel dismisses.
- [ ] `CopyRequestedMessage` and `MoveRequestedMessage` open dialogs with destination pre-filled from the destination-path provider.
- [ ] `CreateFolderRequestedMessage` opens a name-entry dialog; validates inline; new folder becomes cursor and sole selection on success.
- [ ] Two messages arriving while a dialog is open queue FIFO.
- [ ] Operation failures surface non-modally on the source pane.

### 17.13 Stress

- [ ] 100 000-item directory scrolls smoothly at 60 fps.
- [ ] Cursor / page / Home / End respond in under 100 ms.
- [ ] `SelectAllMessage` on 100 000 items publishes exactly one `FileTableSelectionChangedMessage`.
- [ ] Rapid re-binding of `ItemsSource` during scroll produces no stale rows.
- [ ] Non-focused table in a dual-pane scenario stays responsive via bindings while ignoring keyboard messages.
