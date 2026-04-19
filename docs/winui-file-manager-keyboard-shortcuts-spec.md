# KEYBOARD_SHORTCUTS_SPEC.md — Keyboard-First Shortcut and Focus Specification

## Purpose

This document defines the keyboard interaction model for the WinUI 3 dual-pane file manager.

It is intended for the implementation agent.

Goals:

- follow the **Total Commander interaction pattern** where it fits this application
- provide **rich keyboard support** beyond standard desktop defaults
- define **context-sensitive meanings** for keys such as `Enter`, `Delete`, `Esc`, `Tab`, and function keys
- define **primary and secondary shortcuts**
- define **focus, active pane, current item, and selected-items behavior after each action**
- require **tooltips to disclose all supported shortcuts for each command button**
- keep the application usable almost entirely from the keyboard

This document supersedes the previous keyboard shortcut document.

---

## 1. Scope

This document applies to the currently implemented and near-term file manager features:

- dual-pane browsing
- navigate
- refresh
- copy
- move
- rename
- delete
- create folder
- copy full path
- favourites
- inspector toggle
- multi-selection

Rules:
- do **not** mention or plan for features outside this scope
- do **not** include placeholders for actions that will never be implemented
- for files, `Enter` must open the file with the **default Windows application**

---

## 2. Terminology

## 2.1 Active pane

The pane that currently owns file-manager command routing.

There is always exactly one active pane.

## 2.2 Inactive pane

The other pane. It remains visible, may keep its own current item and selection, but does not receive pane commands until activated.

## 2.3 Current item

The item under the pane cursor / focused row inside a pane list.

A pane may have a current item even when it is inactive.

## 2.4 Selected items

The explicit multi-selection set of a pane.

This is **not** the same as the current item.

A pane may have:
- one current item
- zero or more selected items

## 2.5 Keyboard focus owner

The specific control currently receiving key events:
- pane list
- path box
- dialog input
- command button
- etc.

## 2.6 Modal context

A dialog or progress UI that temporarily overrides normal main-window key handling.

---

## 3. Design principles

## 3.1 Keyboard-first means primary, not optional

The application must be fully usable without a mouse for normal work.

Mouse support is secondary.

## 3.2 Follow Total Commander patterns where practical

Adopt Total Commander-style keyboard conventions for file-manager actions whenever they fit the implemented product.

Important examples:
- `F5` copy
- `F6` move
- `Shift+F6` rename
- `F7` create folder
- `F8` / `Delete` delete
- `Tab` switch active pane
- `Backspace` parent directory
- `Ctrl+D` favourites / hotlist
- `Ctrl+I` inspector toggle
- `Insert` as a selection key

## 3.3 One action may have multiple shortcuts

Each action may expose:
- one primary shortcut
- zero or more secondary shortcuts

Examples:
- Delete = `F8`, `Delete`
- Create folder = `F7`, `Ctrl+Shift+N`

## 3.4 Context-sensitive keys are intentional

The same key may do different things depending on context.

This is explicitly required for:
- `Enter`
- `Delete`
- `Esc`
- `Tab`
- `Insert`
- `Space`

## 3.5 Tooltips must list all shortcuts

Every command button in the UI must disclose all supported shortcuts in its tooltip.

Examples:
- `Copy (F5)`
- `Move (F6)`
- `Rename (Shift+F6)`
- `Delete (F8, Delete)`
- `Toggle Inspector (Ctrl+I)`
- `Create Folder (F7, Ctrl+Shift+N)`

If a command is disabled, its tooltip must still show the shortcut.

---

## 4. Shortcut routing priority

If more than one layer could handle a key, use this priority order:

1. modal dialog
2. focused editable input control
3. active pane list
4. main window global command layer

Examples:
- `Enter` in a text box confirms text-entry or dialog behavior first
- `Delete` in a text box edits text, not files
- `Tab` inside a dialog follows dialog focus order first
- `F5` in the main window starts copy only when no modal UI overrides it

---

## 5. Global command set

| Action | Primary shortcut | Secondary shortcuts | Required |
|---|---|---|---|
| Copy | `F5` | — | Yes |
| Move | `F6` | — | Yes |
| Rename in place | `Shift+F6` | — | Yes |
| Create folder | `F7` | `Ctrl+Shift+N` | Yes |
| Delete | `F8` | `Delete` | Yes |
| Toggle inspector | `Ctrl+I` | — | Yes |
| Copy full path | `Ctrl+Shift+C` | — | Yes |
| Favourites / Hotlist | `Ctrl+D` | — | Yes |
| Focus path box | `Ctrl+L` | — | Yes |
| Refresh | `Ctrl+R` | — | Yes |
| Switch active pane | `Tab` | `Shift+Tab` | Yes |
| Exit app | `Alt+F4` | — | Yes |

No placeholder shortcuts should be documented for non-existent features.

---

## 6. Pane and list shortcuts

These shortcuts apply when the active pane list has focus.

## 6.1 Navigation within the list

| Action | Shortcut |
|---|---|
| Move current item up/down | `Up`, `Down` |
| Move by page | `PageUp`, `PageDown` |
| Move to first/last item | `Home`, `End` |
| Open current item | `Enter` |
| Go to parent directory | `Backspace` |
| Alternative parent navigation | `Ctrl+PageUp` |
| Open path entry | `Ctrl+L` |

## 6.2 Selection

| Action | Primary shortcut | Secondary shortcuts |
|---|---|---|
| Toggle current item selection | `Insert` | `Ctrl+Space`, `Space` |
| Extend range up/down | `Shift+Up`, `Shift+Down` | `Shift+PageUp`, `Shift+PageDown` |
| Select all | `Ctrl+A` | — |
| Clear selection | `Ctrl+Shift+A` | `Esc` if nothing higher-priority is active |

Rules:
- `Insert` toggles the current item's selected state and then moves the current item down by one row if possible
- `Ctrl+Space` toggles selection without moving
- `Space` toggles selection without moving
- if `Insert` is used on the last row, the current item remains on the last row

This preserves the Total Commander-style high-speed keyboard selection workflow.

---

## 7. Path box shortcuts

These apply when the active pane path text box has focus.

| Action | Shortcut | Behavior |
|---|---|---|
| Confirm path navigation | `Enter` | Navigate to entered path if valid |
| Cancel path edit | `Esc` | Restore previous path text and return focus to pane list |
| Select all text | `Ctrl+A` | Standard text editing |
| Move by word | `Ctrl+Left/Right` | Standard text editing |
| Delete text | `Delete`, `Backspace` | Standard text editing |
| Switch pane | `Tab`, `Shift+Tab` | Discard uncommitted edit and switch active pane |

Rules:
- file-manager commands must not override normal text-editing behavior inside the path box
- after successful navigation, focus returns to the pane list
- after failed navigation, focus remains in the path box and the error is shown inline or non-modally

---

## 8. Dialog shortcuts

All dialogs must follow consistent desktop rules.

| Action | Shortcut |
|---|---|
| Confirm default action | `Enter` |
| Cancel / close | `Esc` |
| Move focus | `Tab`, `Shift+Tab` |
| Activate focused button | `Space` or `Enter` |

Additional rules:
- `Esc` cancels the dialog
- `Enter` must not dismiss an in-progress operation dialog
- once an operation has completed and the result dialog is shown, `Enter` may close it if the default action is `Close`

---

## 9. Context-sensitive key semantics

## 9.1 Enter

`Enter` is explicitly context-sensitive.

### 9.1.1 In the active pane list

When focus is in the active pane list:

- if the current item is a **directory**:
  - `Enter` navigates into that directory

- if the current item is a **file**:
  - `Enter` opens the file with the **default Windows application**

Implementation note:
- this should be routed through the application service responsible for shell launching
- the command layer must not embed UI-specific launch code directly

### 9.1.2 In the path box

- `Enter` attempts navigation to the typed path
- on success: focus returns to the pane list
- on failure: focus remains in the path box

### 9.1.3 In dialogs

- `Enter` activates the dialog default button

## 9.2 Delete

`Delete` is explicitly context-sensitive.

### 9.2.1 In pane list context

- `Delete` triggers file deletion
- same behavior as `F8`

### 9.2.2 In text input context

- `Delete` edits text only
- it must not start file deletion

### 9.2.3 In dialogs

- `Delete` only acts if the dialog deliberately binds it
- otherwise it should do nothing special

## 9.3 Esc

`Esc` priority:

1. cancel modal dialog
2. cancel progress if allowed
3. clear incremental-search buffer
4. clear pane selection
5. leave path edit and return to list
6. otherwise no action

Do not stack multiple `Esc` effects in one press.

## 9.4 Tab

`Tab` is explicitly context-sensitive.

### 9.4.1 In main pane/list context

- `Tab` switches the active pane
- focus moves to that pane's list

### 9.4.2 In dialogs

- `Tab` follows normal dialog focus order
- it must not switch panes while a modal dialog is active

### 9.4.3 In path box context

- `Tab` switches the active pane
- uncommitted path edits are discarded
- focus moves to the other pane list

---

## 10. Tooltip and command button requirements

## 10.1 Tooltip format

Use the format:

`<Command Name> (<Shortcut 1>, <Shortcut 2>, ...)`

Examples:
- `Delete (F8, Delete)`
- `Create Folder (F7, Ctrl+Shift+N)`

## 10.2 Disabled commands

Even when a command button is disabled, the tooltip must still include the shortcut.

## 10.3 Multiple surfaces

If the same action appears in multiple UI locations, use the same shortcut text everywhere.

---

## 11. Focus and selection state model

This section defines what must happen after actions.

## 11.1 General rules

Unless otherwise specified:

- the pane that launched the action remains the **active pane**
- after dialogs close successfully or are cancelled, focus returns to the **list of the launching pane**
- the inactive pane remains inactive
- actions in one pane should disturb the other pane as little as possible
- preserve the inactive pane selection whenever practical
- do not auto-activate the destination pane after copy or move
- distinguish clearly between:
  - active pane
  - focus owner
  - current item
  - selected-items set

## 11.2 Best-practice selection rules

Use these principles:

- **Copy** should usually preserve the source selection, because the source items still exist and the user may want to reuse the selection
- **Move** should clear source selection, because the moved items no longer exist there
- **Delete** should clear source selection, because deleted items no longer exist
- **Non-destructive inspection actions** should preserve selection
- **Inactive pane selection** should remain unchanged unless that pane's contents were directly modified or its selected items disappeared
- **Do not clear destination-pane selection after copy or move**, unless the destination pane is itself the launching pane or its selected items became invalid

These are the default rules for this application.

---

## 12. Post-action state rules by command

## 12.1 Switch active pane (`Tab` / `Shift+Tab`)

### Success
- active pane changes to the other pane
- focus goes to that pane's list
- current item in the target pane is preserved if valid
- selected-items set in both panes is preserved

---

## 12.2 Navigate into directory (`Enter` on directory)

### Success
- launching pane remains active
- focus returns to launching pane list
- current item becomes:
  - first item in the new directory, if any
  - otherwise no current item
- selected-items set in the launching pane is cleared
- inactive pane unchanged, including its selection

### Failure
- launching pane remains active
- focus remains in launching pane list
- previous current item and selection remain unchanged

---

## 12.3 Open file (`Enter` on file)

### Success
- launching pane remains active
- focus remains in or returns to launching pane list
- current item remains unchanged
- selection remains unchanged in both panes

### Failure
- launching pane remains active
- focus returns to launching pane list
- selection remains unchanged
- show a non-modal or lightweight error

Opening a file must not disturb pane state.

---

## 12.4 Navigate to parent (`Backspace` or `Ctrl+PageUp`)

### Success
- launching pane remains active
- focus returns to launching pane list
- selected-items set is cleared
- current item should try to land on the directory that was just exited, if visible in the parent directory
- if not possible, choose the nearest logical item

### Failure
- no pane switch
- selection unchanged
- focus unchanged

---

## 12.5 Navigate via path box (`Ctrl+L`, then `Enter`)

### On path-box focus
- active pane remains active
- focus moves to active pane path box
- selection in pane list remains unchanged until navigation is committed

### Success after `Enter`
- active pane unchanged
- focus returns to launching pane list
- launching pane selection cleared
- current item becomes first item or none if folder empty
- inactive pane unchanged

### Failure after `Enter`
- active pane unchanged
- focus remains in path box
- no pane switch
- old pane selection remains unchanged until successful navigation or explicit cancel

### `Esc` in path box
- restore previous path text
- focus returns to launching pane list
- selection remains unchanged

---

## 12.6 Refresh (`Ctrl+R`)

### Success
- active pane unchanged
- focus remains in launching pane list
- current item preserved if still present
- selected-items set preserved for items that still exist
- missing selected items are removed from selection
- inactive pane unchanged unless explicitly refreshed too

### Note: passive (stream-driven) updates
- routine pane updates after copy/move/delete or any external change happen automatically through the directory-change stream and do not require `Ctrl+R`
- `Ctrl+R` remains available as an explicit "rebuild from disk" action (e.g. after removable media changes) and is the only refresh that restores the current-item by name

### Special case: current directory deleted externally
- if the pane's current directory no longer exists, the refresh (or a stream-invalidation event) rolls back to the highest existing ancestor directory
- focus remains in the launching pane list
- active pane does not change

### Failure
- active pane unchanged
- focus remains in launching pane list
- previous view state preserved as much as practical

---

## 12.7 Copy (`F5`)

### Before execution
- launching pane is the source pane
- opposite pane is the default destination context
- active pane remains the source pane

### Success
After the copy operation completes successfully:

- **source pane remains active**
- focus returns to **source pane list**
- source pane selection is **preserved**
- source pane current item remains on the same current item if still valid
- destination pane remains **inactive**
- destination pane selection is **preserved**
- destination pane current item is preserved if still valid

This is the default best-practice behavior because copy is non-destructive to the source and should not disturb existing work in the destination pane.

### Example
If the user copies from left to right:
- left pane stays active
- focus returns to the left list
- left selection remains as it was
- right pane remains inactive
- right selection remains as it was

This example is mandatory.

### Cancel
- source pane remains active
- focus returns to source pane list
- source selection preserved
- destination selection preserved

### Failure with partial success
- source pane remains active
- focus returns to source pane list after the result summary closes
- source selection preserved
- destination selection preserved unless the destination pane itself was refreshed and invalid selections had to be dropped
- do not activate destination pane automatically

---

## 12.8 Move (`F6`)

### Before execution
- launching pane is the source pane
- opposite pane is the default destination

### Success
After a successful move from source pane to opposite pane:

- **source pane remains active**
- focus returns to **source pane list**
- source pane selection is **cleared**
- source pane current item moves to the nearest logical surviving item after the moved range disappears
- destination pane remains inactive
- destination pane selection is **preserved**
- destination pane current item is preserved if valid

This is the default best-practice behavior because the source items are gone, while the destination pane may already contain an unrelated working selection.

### Example
If the user moves from left to right:
- left pane stays active
- focus returns to the left list
- left selection becomes empty
- right pane remains inactive
- right selection remains as it was

This example is mandatory.

### Cancel
- source pane remains active
- focus returns to source list
- source selection preserved
- destination selection preserved

### Failure with partial success
- source pane remains active
- focus returns to source list after the result summary closes
- source selection is cleared if the move materially changed the source pane contents
- otherwise source selection may be preserved
- destination selection remains preserved unless invalidated by refresh
- do not auto-activate the destination pane

Recommended rule:
- if any source items were actually moved away, clear source selection

---

## 12.9 Rename in place (`Shift+F6`)

### Start
- launching pane remains active
- focus moves to inline rename editor or rename dialog input
- if multiple items were selected, collapse selection to the current item before rename begins

### Success
- launching pane remains active
- focus returns to launching pane list
- renamed item remains the current item
- renamed item becomes the **sole selected item**

### Cancel
- launching pane remains active
- focus returns to launching pane list
- pre-rename current item restored
- pre-rename selection preserved

### Failure
- keep rename UI open if user can fix the name
- otherwise close and return focus to list, preserving original selection

---

## 12.10 Create folder (`F7`, `Ctrl+Shift+N`)

### Start
- launching pane remains active
- create-folder dialog or inline editor opens

### Success
- launching pane remains active
- focus returns to launching pane list
- newly created folder becomes current item
- newly created folder becomes the **sole selected item**
- inactive pane unchanged

### Cancel
- launching pane remains active
- focus returns to launching pane list
- previous selection preserved

### Failure
- if validation error is fixable, keep create-folder UI open
- otherwise return focus to list and preserve original selection

---

## 12.11 Delete (`F8`, `Delete`)

### Start
- launching pane remains active
- delete confirmation dialog opens

### Success
- launching pane remains active
- focus returns to launching pane list
- selected-items set in launching pane is cleared
- current item moves to the nearest logical surviving item
- inactive pane remains inactive
- inactive pane selection remains unchanged unless that pane was explicitly refreshed and invalid selections had to be dropped

### Cancel
- launching pane remains active
- focus returns to launching pane list
- original selection preserved

### Failure with partial success
- launching pane remains active
- focus returns to launching pane list after the result summary closes
- selection cleared in launching pane if items were actually removed
- current item moves to nearest surviving item
- inactive pane selection preserved unless invalidated by refresh

---

## 12.12 Toggle Inspector (`Ctrl+I`)

### Success
- active pane unchanged
- focus remains where it was before toggle
- selection unchanged
- inspector visibility toggles

Inspector toggle is a non-destructive UI action and must not disturb pane state.

---

## 12.13 Copy full path (`Ctrl+Shift+C`)

### Success
- active pane unchanged
- focus remains where it was before the command
- selection unchanged
- current item unchanged

### Failure
- same as success, but show lightweight error feedback

This action must not disturb pane state.

---

## 12.14 Open favourites (`Ctrl+D`)

### Open
- active pane unchanged
- favourites dialog/popup opens
- current pane selection unchanged while the dialog is open

### Choose favourite successfully
- launching pane remains active
- focus returns to launching pane list
- launching pane navigates to chosen favourite
- launching pane selection is cleared
- current item becomes first item or none if folder empty
- inactive pane unchanged

### Cancel
- active pane unchanged
- focus returns to launching pane list
- selection unchanged

---

## 12.15 Toggle selection (`Insert`, `Ctrl+Space`, `Space`)

### `Insert`
- launching pane remains active
- focus remains in launching pane list
- current item's selected state toggles
- current item then moves to next row if possible

### `Ctrl+Space`
- launching pane remains active
- focus remains in list
- current item stays in place
- current item's selected state toggles

### `Space`
- same behavior as `Ctrl+Space`
- no movement

---

## 12.16 Select all (`Ctrl+A`)

### Success
- launching pane remains active
- focus remains in launching pane list
- all eligible items in that pane become selected
- current item remains unchanged
- inactive pane unchanged

---

## 12.17 Clear selection (`Ctrl+Shift+A` or `Esc` fallback)

### Success
- launching pane remains active
- focus remains in launching pane list
- selected-items set becomes empty
- current item remains unchanged
- inactive pane unchanged

---

## 13. Failure and cancellation policy

## 13.1 Cancelled before execution
If the user opens a dialog and cancels before the operation starts:
- return focus to launching pane list
- preserve original selection
- preserve active pane

## 13.2 Cancelled during execution
If the user cancels during a running operation:
- return focus to launching pane list after the progress/result UI closes
- keep source pane active
- preserve or clear selection based on whether the source pane was materially changed

## 13.3 Completed with errors
After a completed operation with warnings/errors:
- keep source pane active
- show result summary
- after closing summary, return focus to source pane list
- preserve inactive pane selection whenever practical

---

## 14. Command button and menu disclosure rules

## 14.1 Tooltip text is mandatory

Buttons must show shortcuts in tooltips.

## 14.2 Multiple shortcuts must be listed in priority order

Use:
1. primary shortcut first
2. secondary shortcut second

Example:
- `Delete (F8, Delete)`

## 14.3 One action, many triggers

The same action must be callable from:
- keyboard shortcut
- button
- menu item if present
- internal automation/test hook

All triggers must route to the same underlying command handler.

---

## 15. Required implementation notes for the agent

## 15.1 Use a centralized shortcut map

Do not scatter shortcut handling through unrelated code-behind files.

Create a central source of truth for:
- command id
- display name
- primary shortcut
- secondary shortcuts
- tooltip text
- active contexts

## 15.2 Keep shortcut registration separate from command execution

The command layer must not depend on WinUI controls.

The UI layer should translate key gestures into command invocations.

## 15.3 Tests must verify shortcut routing and state restoration

Test categories should include:
- key gesture -> command mapping
- context-sensitive key behavior
- focus restoration after command completion
- selection clearing/preservation rules
- active pane invariants

---

## 16. Mandatory examples

These examples are normative.

## 16.1 Copy from left pane to right pane
After success:
- left pane remains active
- focus returns to left pane list
- left selection remains as it was
- right pane remains inactive
- right selection remains as it was

## 16.2 Move from left pane to right pane
After success:
- left pane remains active
- focus returns to left pane list
- left selection is empty
- right pane remains inactive
- right selection remains as it was

## 16.3 Delete selected files in active pane
After success:
- same pane remains active
- focus returns to same pane list
- selection becomes empty
- current item moves to nearest surviving item

## 16.4 Toggle Inspector
After toggle:
- same pane remains active
- focus remains where it was
- selection unchanged

## 16.5 Cancel a modal command dialog before execution
After cancel:
- same pane remains active
- focus returns to same pane list
- selection unchanged

## 16.6 Open a file with `Enter`
After success:
- same pane remains active
- focus returns to or remains in the same pane list
- current item unchanged
- selection unchanged in both panes

---

## 17. Summary of required shortcuts

| Action | Shortcuts |
|---|---|
| Switch pane | `Tab`, `Shift+Tab` |
| Parent directory | `Backspace`, `Ctrl+PageUp` |
| Path focus | `Ctrl+L` |
| Open current item | `Enter` |
| Copy | `F5` |
| Move | `F6` |
| Rename | `Shift+F6` |
| Create folder | `F7`, `Ctrl+Shift+N` |
| Delete | `F8`, `Delete` |
| Toggle inspector | `Ctrl+I` |
| Copy full path | `Ctrl+Shift+C` |
| Favourites | `Ctrl+D` |
| Refresh | `Ctrl+R` |
| Toggle selection | `Insert`, `Ctrl+Space`, `Space` |
| Select all | `Ctrl+A` |
| Clear selection | `Ctrl+Shift+A` |
| Cancel/close | `Esc` |
| Exit app | `Alt+F4` |

---

## 18. Final rule

If there is a conflict between:
- a visually simpler implementation
- and a more precise keyboard workflow

the agent must choose:

**the more precise keyboard workflow**

This application is explicitly keyboard-first.
