# WinUI file pane control spec — use WinUI.TableView

## Decision

Use **WinUI.TableView** for the file pane detailed view.

Do **not** use `CommunityToolkit.Labs` `DataTable` for this screen.

## Why

This screen needs all of the following at the control level:
- read-only row display
- explicit columns
- built-in column sorting
- built-in column resizing
- extended row selection
- compact dense layout
- acceptable behavior with very large item counts

`DataTable` is still an experimental Labs component and is a worse fit here because it is intentionally lighter-weight and does not provide built-in sorting. It is better suited to simple table layouts where the app is willing to own more behavior itself.

`WinUI.TableView` is the better fit because it is an actively shipped control with stable releases, is derived from `ListView`, is aimed at large item sets, and already provides the grid behaviors this file manager needs.

## Scope

This spec applies only to the main file list in each pane.

The path box, status bar, command routing, dialogs, and file operations remain outside this control.

## Required package choice

- Package: `WinUI.TableView`
- Version policy: use the **latest stable** version available at implementation time
- Baseline at time of writing: `1.4.0`

## Required usage profile

Use `TableView` as a **read-only row-oriented file list**.

### Mandatory settings
- `AutoGenerateColumns = false`
- `IsReadOnly = true`
- row selection, not cell selection
- multiple or extended selection enabled
- compact density enabled if available
- explicit columns only
- no runtime column auto-generation

### Explicitly disabled in v1
- editing
- row details
- export UI
- built-in filter flyouts
- clipboard export features
- column reordering
- per-cell selection mode
- grouped view
- aggregate rows
- inline validation UI

## Column model

Declare columns explicitly and keep them stable.

Required columns:
- Name
- Extension
- Size
- Modified
- Attributes
- FileId

### Width policy
- `Name`: star width, minimum width enforced
- `Extension`: fixed width
- `Size`: fixed width, right aligned
- `Modified`: fixed width
- `Attributes`: fixed width
- `FileId`: fixed width

### Column rules
- directories sort before files
- `..` parent entry stays first and is excluded from normal sort semantics
- `Name` is the only column allowed to use a template if an icon is shown
- all other columns should be plain bound columns
- no wrapped text in rows
- single-line dense rows only

## Sorting

Sorting must use the control's built-in sorting support.

### Required behavior
- single-column sorting only in v1
- clicking the same header toggles ascending/descending
- clicking a different header switches active sort column and starts ascending
- each pane owns its own sort state
- default sort: parent entry first, then directories, then files, then name ascending

### Forbidden
- no header-button hacks outside the control
- no mirrored fake header row
- no manual sort glyph management outside normal control customization
- no code-behind sorting logic in pages or windows

Sorting behavior should be driven from the pane view-model and mapped to the control through a dedicated presentation wrapper/service if needed.

## Resizing

Use the control's built-in column resizing.

### Required behavior
- user can resize columns directly from the header
- widths may be persisted as pane settings
- minimum widths must be enforced to keep the grid usable

### Forbidden
- no `GridSplitter`
- no extra header grid for width coordination
- no pointer-event width synchronization
- no copying widths into realized rows
- no XAML code-behind resize hacks

## Selection and focus

Use row selection only.

### Required behavior
- extended multi-select
- keyboard selection must match the file-manager spec
- current item and selected items remain application concepts owned by the pane view-model
- focus returns to the file list after dialogs and commands
- clicking inside a pane activates that pane

### Forbidden
- no cell-selection-driven UX for the file manager
- no business logic hidden in control events

## Performance rules for up to 100_000 files

The control choice alone is not enough. The pane architecture must still be optimized.

### Required
- asynchronous enumeration
- batched population of the bound collection
- no synchronous metadata loading on the UI thread
- stable lightweight row view-models
- avoid heavy converters in hot columns
- avoid template columns except where strictly necessary
- refresh and re-sort only through the pane data source, not by rebuilding the whole visual tree for minor changes

### Strong recommendation
Use a flat row collection with incremental/batched updates and keep the row template minimal. Do not attach expensive behaviors to each cell.

## No-hacks rule

This control is accepted only if it works with its built-in behaviors and normal templating/styling.

The implementation must not rely on:
- page-level code-behind event hacks
- custom pointer plumbing for resize/sort
- duplicated header/body layout systems
- manual width mirroring across visual elements
- special-case logic that exists only to compensate for the control

If the agent cannot satisfy the file-manager UX with this profile, the fallback should be a custom `ListView`/`ItemsRepeater` based pane implementation, not a growing collection of `TableView` workarounds.

## Acceptance criteria

This choice is accepted only if all of the following are true:
- sorting works for the required columns
- columns are resizable with no custom splitter logic
- row selection is stable and keyboard-friendly
- the pane remains responsive with very large directories
- the implementation stays read-only in v1
- there is no page/window XAML code-behind for grid behavior
- the control can be replaced later without changing application-layer contracts
