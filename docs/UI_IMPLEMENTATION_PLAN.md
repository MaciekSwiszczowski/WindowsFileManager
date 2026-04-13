# UI_IMPLEMENTATION_PLAN.md — Detailed UI Implementation Plan for the WinUI 3 File Manager

## Purpose

This document is a **UI-focused implementation plan** for the autonomous coding agent.
It complements the existing product spec, agent brief, and bootstrap document.

It answers the question: **how should the WinUI 3 user interface actually be built, structured, and phased** so the agent does not have to invent core UX decisions.

This document is intentionally prescriptive.

---

## 1. UI goals

The UI must optimize for:

1. **Keyboard-first operation**
2. **Fast navigation in large NTFS directories**
3. **Clear active-pane semantics**
4. **Predictable bulk-operation workflows**
5. **Low UI complexity in v1**
6. **Future extensibility for low-level NTFS functionality**

The UI is not intended to mimic Total Commander visually one-to-one.
It should mimic the **interaction model**, not the exact appearance.

---

## 2. UI scope for v1

The UI must implement these user-visible features in v1:

- Dual-pane layout
- Folder navigation
- Refresh
- Copy / move / rename / delete / create folder
- Multi-selection
- Bulk operations
- Favourite folders
- View properties
- Copy full path
- Display NTFS FileId
- Optional parallel-operation switch
- Progress and result dialogs
- Keyboard shortcuts for all implemented commands

The UI must **not** implement in v1:

- Embedded preview pane
- Archive browsing
- Explorer shell integration
- Tabs
- Custom theming system
- Plugin system
- Advanced search panel
- Tree view sidebar
- Recycle Bin integration
- Permission editor
- ADS editor
- USN journal tools

---

## 3. Core UI decision: custom pane control, not a naive stock file browser

The agent must **not** build the main file pane as a random collection of controls stitched together ad hoc.

The UI should be built around a dedicated custom control or feature module conceptually named:

- `FilePaneView`
- `FilePaneViewModel`
- `FilePaneSelectionModel`
- `FilePaneNavigationState`

A pane is a full interaction surface, not just a list.

Each pane owns:

- current directory
- item source
- current cursor item
- selection set
- sort state
- pending incremental-search buffer
- loading state
- error state

---

## 4. Main window composition

## 4.1 Window layout

The main window should use this vertical structure:

1. Top command strip / minimal toolbar
2. Main two-pane region
3. Bottom status bar

Structure:

```text
+---------------------------------------------------------------+
| Menu/Command Bar                                              |
+---------------------------------------------------------------+
| Left pane                         | Right pane                 |
| path bar                          | path bar                   |
| item list                         | item list                  |
| inline load/error indicators      | inline load/error indicators|
+---------------------------------------------------------------+
| Status bar / operation hints / active pane / item counts      |
+---------------------------------------------------------------+
```

## 4.2 No left navigation tree in v1

Do not add a tree view sidebar in v1.
It complicates focus, layout, keyboard rules, and synchronization.
Two-pane file managers work well without it.

## 4.3 No breadcrumb-heavy top chrome

Use a **simple editable path box** rather than a complex breadcrumb control in v1.
The path box is easier for keyboard-heavy users and simpler for the agent to implement correctly.

Recommended per pane:

- small path row
- editable path textbox
- small buttons only if needed for mouse parity, not as the primary interaction model

---

## 5. Pane internal composition

Each pane should have this internal structure:

```text
+------------------------------------------+
| [Drive ▼] [TextBox path]                |
+------------------------------------------+
| Header row                               |
| Name | Ext | Size | Modified | Attr | Id |
+------------------------------------------+
| [..] parent entry (if not at root)       |
| Scrollable item surface                  |
| files and directories                    |
| virtualized                              |
+------------------------------------------+
| Optional inline pane footer              |
| loading / empty / error                  |
+------------------------------------------+
```

## 5.1 Path row

Responsibilities:

- drive selector ComboBox (leftmost, shows NTFS drive letters)
- show current absolute path in an editable TextBox
- allow direct path entry
- allow Enter to navigate
- select-all on focus via shortcut if practical
- reject non-NTFS targets with clear feedback
- drive ComboBox auto-syncs when the path changes to a different volume
- on startup, both panes load the first NTFS drive's root

Do not overcomplicate path history in v1.
A simple backstack can exist internally, but it does not need dedicated UI controls yet.

## 5.2 Header row

Required columns in v1:

- Name
- Extension
- Size
- Modified
- Attributes
- FileId

Rules:

- directories show blank extension unless explicitly chosen otherwise
- directories show blank or special size placeholder
- FileId column may be blank while loading if metadata is deferred
- FileId must display the file identifier only, in hex

Recommended width policy:

- Name = `*` (star-sized, fills remaining space, `MinWidth="100"`)
- Extension = `40px` fixed
- Size = `70px` fixed, right-aligned
- Modified = `120px` fixed
- Attributes = `50px` fixed
- FileId = `100px` fixed

ListView items **must** set `HorizontalContentAlignment="Stretch"` via `ItemContainerStyle` to ensure star-sized columns fill the available width. Without this, items auto-size to content and the Name column collapses.

The header row and item template grids must use identical column definitions to stay aligned.

Do not implement column drag-reordering in v1.
Optional in v1:
- resize columns
- sort by clicking header

---

## 6. Primary control choice for the file list

## 6.1 Recommended approach

Use **ItemsRepeater-based custom pane implementation** if the agent can handle the extra work cleanly.

Reason:
the product needs custom policy for:
- active pane
- cursor item
- keyboard-first selection behavior
- incremental search
- custom focus behavior
- future low-level operations and metadata
- strict dual-pane semantics

That is exactly the kind of scenario where a simple stock `ListView` often becomes constraining.

## 6.2 Acceptable fallback

If the agent is at risk of producing an unstable custom implementation, `ListView` is acceptable **only as an implementation accelerator** for v1.

If `ListView` is used:
- the agent must still preserve the explicit pane selection/focus model
- the command layer must not depend on `ListView`
- the control choice must remain replaceable later

## 6.3 Decision rule for the agent

Use this rule:

- Prefer **ItemsRepeater** when implementing the main pane if the agent can keep the code modular.
- Fall back to **ListView** only if that materially reduces delivery risk for v1.

### 6.4 Actual implementation (v2 — DynamicData)

`ListView` with **DynamicData** `SourceCache` backing:

- `FilePaneViewModel` owns a `SourceCache<FileEntryViewModel, string>` keyed by `UniqueKey` (full path or `".."` for parent).
- `SortAndBind(out _sortedItems, comparerObservable)` produces a `ReadOnlyObservableCollection<FileEntryViewModel>` assigned to `ListView.ItemsSource`.
- On navigation, `SourceCache.Edit(updater => { updater.Clear(); ... })` replaces items in one batch.
- After operations, `SourceCache.Remove(keys)` / `AddOrUpdate(items)` provide delta updates.
- Re-sorting is reactive: pushing a new `FileEntryComparer` into `BehaviorSubject<IComparer<>>` triggers automatic re-sort.
- `SelectionMode="Extended"` for native single, range, and toggle selection.
- `SelectionChanged` syncs `ListView.SelectedItems` → `FileEntryViewModel.IsSelected` and activates the pane.
- Space/Insert toggle `IsSelected` on the ViewModel and sync back to `ListView.SelectedItems`.
- Column header `Button` elements support click-to-sort with ▲/▼ indicators.
- **`CommunityToolkit.WinUI.Controls.GridSplitter`** between header columns; row grids mirror header `ColumnDefinition` widths after each resize. **`FilePaneDisplayOptions.EnableColumnResize`** turns splitters on or off.
- `ItemContainerStyle` sets `HorizontalContentAlignment="Stretch"` for proper column layout.
- Custom `ListViewItemBackgroundSelected` resources in `App.xaml` ThemeDictionaries improve selection visibility.

---

## 7. Active pane model

The application must always have exactly one **active pane**.

Rules:

- Only one pane is active at a time.
- The active pane is visually distinct.
- Commands operate on the active pane unless explicitly target-based.
- Copy/move target is normally the opposite pane's current directory.
- Focus changes should usually imply active-pane change.
- Clicking inside a pane activates it.
- `Tab` toggles active pane.
- `Shift+Tab` should also be supported if practical.

Visual cues for active pane:

- slightly stronger border
- stronger header background
- status bar label: `Active: Left` or `Active: Right`

Do not rely only on subtle color changes.
The state must be obvious.

---

## 8. Focus model

## 8.1 Keyboard focus contract

Focus must behave predictably:

- `Tab` switches between major zones, primarily between panes
- Arrow keys move inside the file list
- Enter activates the current item
- Space toggles selection when supported
- Focus should return to the file list after most commands complete
- Closing a dialog should restore focus to the pane that launched it

## 8.2 Pane-local focus strategy

Within a pane, there are two important interactive areas:

- path textbox
- file list

Rules:

- `Ctrl+L` focuses the active pane path textbox
- `Esc` in path textbox returns focus to list without navigating
- after successful path navigation, focus returns to the list
- list focus should be stable across refreshes when possible

## 8.3 No accidental focus traps

The UI must not trap focus inside:
- dialogs
- command bar
- status bar
- transient popups

---

## 9. Selection model

Selection must be explicit and independent from keyboard focus implementation details.

The pane needs a separate `SelectionModel` abstraction.

**Command target convention**: When no items are explicitly selected (via Space/Insert), commands operate on the current (focused) item. This follows Total Commander convention and ensures toolbar buttons always have a target.

## 9.1 Required behaviors

- single selection
- multi-selection
- range selection
- toggle selection
- select all
- clear selection
- maintain cursor/current item separately from selected set
- `..` parent entry is never a valid command target

## 9.2 Keyboard rules

Recommended v1 rules:

- `Up` / `Down`: move cursor (native `ListView` behavior with `SelectionMode="Extended"`)
- `Shift+Up` / `Shift+Down`: extend range (native `ListView` behavior)
- `Space` / `Insert`: toggle selected state of current item and advance cursor
- `Ctrl+A`: select all
- `Esc`: clear selection if any; otherwise clear incremental-search buffer
- `Home` / `End`: jump to first / last item
- `Enter`: open folder / navigate to parent if on `..` entry
- `Backspace`: navigate to parent directory

## 9.3 Mouse rules

Even though the app is keyboard-first, basic mouse parity is still needed:

- single click selects current item (native `ListView` with `SelectionMode="Extended"`)
- double click opens directory / default action
- `Ctrl+click` toggles item (native `ListView` behavior)
- `Shift+click` selects range (native `ListView` behavior)
- clicking on any area within a pane (including empty space) activates that pane

### 9.4 Implementation notes

Selection uses native `ListView` `SelectionMode="Extended"` behavior. `IsItemClickEnabled` is **not** set, so that standard selection semantics apply. The `SelectionChanged` event is used for syncing the ViewModel's `CurrentItem` and activating the pane. The `Tapped` event on the pane's root `Border` handles clicks on empty space.

---

## 10. Navigation UX

## 10.1 Directory navigation

Directory navigation sources:

- Enter on directory
- Backspace for parent directory
- path entry and Enter
- favourite activation
- command invocation from command bar

## 10.2 File activation

For v1, file activation must be explicitly defined.

Recommended v1 rule:
- Enter on a file does **not** launch it by default unless you explicitly choose to support `Open externally`.
- If supported, use a clearly separated command such as `Ctrl+Enter` or `Alt+Enter` for properties only.

Reason:
opening arbitrary files is not central to the initial scope and adds shell concerns.

## 10.3 Refresh behavior

Refresh must preserve as much state as possible:

- same directory
- same sort order
- same active pane
- current item if still present
- selection if items still present and identity can be resolved

If current item disappears:
- move cursor to nearest surviving item
- otherwise first item
- otherwise no selection in empty folder

---

## 11. Sorting

v1 sorting should be intentionally small.

Required:
- sort by Name ascending/descending
- optional sort by Modified and Size if cheap

Recommended default:
- directories first
- then files
- name ascending
- case-insensitive natural-ish compare if practical

Do not add multi-column sorting in v1.

The sort state belongs to the pane.
Each pane may have a different sort state.

---

## 12. Incremental search

The app should support a lightweight incremental search inside the active pane.

Behavior:

- typing while the list has focus starts or continues a search buffer
- the pane moves current item to the first match
- the buffer resets after a short inactivity timeout
- Backspace edits the buffer
- Esc clears it

v1 matching rule:
- prefix match against display name
- case-insensitive

Optional:
- show the current incremental search buffer in the status bar

Do not build a full search panel for v1.

---

## 13. Virtualization and large folders

The main item surface must be designed for large folders.

UI implications:

- the item surface must be virtualized
- metadata loading may be staged
- FileId may be loaded asynchronously if needed
- avoid blocking the UI thread on directory enumeration or metadata extraction
- avoid rebuilding the entire visual tree on small updates

Practical UI rule:
- render the shell of the pane quickly
- show directory contents progressively if necessary
- indicate loading inline, not through modal UI

---

## 14. File row design

Each row should be simple and dense.

## 14.1 Row structure

Row content:
- icon slot
- name
- extension
- size
- modified date/time
- attributes
- FileId

## 14.2 Density

Target a compact dense layout suitable for engineers.
Do not create a touch-first oversized layout.

## 14.3 Icons

v1 options:
- minimal generic file/folder icons
- or no icons if that speeds delivery

Icons are lower priority than keyboard precision and performance.

## 14.4 Text overflow

Required:
- Name column truncates with ellipsis
- FileId truncates only if column is too small
- path textbox shows full path by scrolling, not truncating

---

## 15. Empty, loading, and error states

Each pane must support three lightweight inline states.

## 15.1 Empty state

Show a subtle inline message:
- `Folder is empty`

## 15.2 Loading state

Show:
- inline progress ring or small loading text
- keep pane structure visible
- avoid replacing the whole pane with a splash-like surface

## 15.3 Error state

Examples:
- access denied
- path not found
- non-NTFS
- locked directory handle issue
- enumeration failure

Show:
- short inline error summary
- optional action: retry
- status bar echo
- keep the rest of the window usable

Do not crash the entire UI because one pane failed to enumerate.

---

## 16. Command bar / top area

The top area should be minimal.

Recommended contents:
- app title
- a minimal command bar with a small set of visible commands
- maybe a compact overflow menu for less common commands

Visible primary commands (icon-only, with tooltips showing keyboard shortcuts):
- Copy (F5)
- Move (F6)
- Rename (F2)
- Delete (F8)
- New Folder (F7)
- Refresh (Ctrl+R)
- Favourites (Ctrl+B)
- Properties (Alt+Enter)
- Copy Path (Ctrl+Shift+C)
- Toggle Theme

### Actual implementation (v2)

`CommandBar` with `DefaultLabelPosition="Collapsed"`, `HorizontalAlignment="Left"`, `OverflowButtonVisibility="Collapsed"`. All commands are primary buttons (no hidden overflow). Theme toggle is a primary button, not in secondary commands. Tooltips show shortcut keys.

Do not make the command bar the primary workflow.
It is secondary to keyboard shortcuts.

---

## 17. Status bar design

The status bar is important in this kind of app.

It should show:

- active pane
- current directory or abbreviated path
- selected item count
- selected total size if cheap to compute
- current item count in pane
- transient operation hints
- incremental search buffer if active
- command error feedback (e.g. `Error: <message>`)

Examples:

- `Left active | 124 items | 3 selected`
- `Right active | Loading...`
- `Copy: 42/310`
- `Search: rep`
- `Error: Access denied`

---

## 18. Dialog strategy

The app needs a small set of dialogs with predictable behavior.

## 18.1 Required dialogs in v1

- Copy dialog
- Move dialog
- Delete confirmation
- Create folder dialog
- Properties dialog
- Operation progress dialog
- Operation result summary dialog
- Favourites management dialog or lightweight flyout

## 18.2 Dialog style rules

- modal only when the decision is required
- compact
- keyboard-friendly
- default button must be sensible
- Escape cancels
- Enter confirms when safe
- focus starts in the primary input

---

## 19. Copy / Move dialog UX

The copy and move dialog should not be overdesigned.

Fields:

- source summary (read-only)
- destination path textbox
- optional checkbox: use parallel execution
- optional overwrite policy selector
- buttons: OK / Cancel

Behavior:
- default destination = opposite pane current path
- validate NTFS before execution
- validate destination path before start
- keep the last used options if useful and safe

Do not build a wizard.

---

## 20. Delete UX

Delete is high-risk.

v1 recommended behavior:

- confirmation dialog for delete
- show item count and whether directories are included
- no Recycle Bin integration in v1
- clear wording that delete is permanent in v1

Optional:
- allow disabling confirmation later, but not required in v1

---


## 22. Properties UI

The properties UI can be a dialog or side panel.
Dialog is simpler for v1.

Required fields:

- Name
- Full path
- Type (file / directory / link)
- Size
- Created / modified timestamps
- Attributes
- NTFS FileId
- Selection summary when multiple items are selected

For multiple selection:
- aggregated count
- aggregated size if cheap
- FileId omitted or replaced with `Multiple items`

Do not add ACL editing in v1.

---

## 23. Favourite folders UI

Favourites are important enough to deserve explicit UI.

## 23.1 Required features

- add current pane directory to favourites
- remove favourite
- open favourite into active pane
- persisted across app restarts

## 23.2 UI form

Recommended v1:
- keyboard-openable popup or dialog
- simple list
- optional alias/name
- open/remove actions

### Actual implementation (v1)

Favourites are implemented as a `MenuFlyout` attached to the Favourites `AppBarButton`:
- Top item: "Add current folder" (`Ctrl+D`) — adds the active pane's current path
- Separator
- Dynamic list of saved favourites, each showing `DisplayName — Path`
- Clicking a favourite navigates the active pane to that path via `OpenFavouriteCommand`
- `Ctrl+B` opens the flyout
- The flyout rebuilds its items dynamically in the `Opening` event from `ViewModel.Favourites`

Minimal fields per favourite:
- display name
- full path

## 23.3 Rules

- reject non-NTFS paths
- on startup, if a favourite no longer exists, keep it but mark invalid
- opening an invalid favourite should show a controlled error

---

## 24. Parallel execution switch UI

This needs to be visible but not prominent.

Recommended placement:
- in Copy and Move dialogs
- maybe also in app settings later, but not required in v1

Label example:
- `Use parallel file operations`

Optional explanatory hint:
- `May improve throughput for independent files`

Do not expose deep tuning controls in the main UI in v1.
Only one toggle is required.
Any advanced tuning can remain internal or hidden.

---

## 25. Locked files and partial failure UX

The UI must explicitly handle operational ambiguity.

## 25.1 During operation

If one item fails:
- do not instantly collapse the whole operation
- apply current error policy
- update progress dialog
- continue or stop according to the selected rule

## 25.2 Progress dialog fields

- current operation name
- current item path or abbreviated path
- progress count
- current speed optional
- cancel button
- optional expand details area

## 25.3 Result summary dialog

At completion show:
- success count
- warning count
- failure count
- cancelled status if relevant
- expandable error list

Per failed item show:
- path
- operation
- error category
- raw code optional
- short explanation

---

## 26. Accessibility and keyboard cues

The UI must not treat accessibility as an afterthought.

Required:

- visible focus cues
- complete keyboard navigation
- sensible tab order
- keyboard accelerators visible in tooltips or command surfaces where practical
- accessible labels for path boxes, status messages, and dialogs
- no color-only communication for active pane or errors

The agent must ensure:
- controls have automation names where needed
- dialogs announce purpose clearly
- the active pane is inferable through both visuals and accessible naming

---

## 27. Styling guidance

## 27.1 Overall look

Use a clean, restrained WinUI 3 desktop look.
Do not attempt a flashy custom theme in v1.

## 27.2 Density and spacing

Use moderate desktop density:
- compact rows
- restrained padding
- no oversized cards
- no mobile-style spacing

## 27.3 Color usage and theming

Dark theme is the default, set via `root.RequestedTheme = ElementTheme.Dark` in the Window constructor.
**Do not** set `RequestedTheme` in `App.xaml` — this prevents dynamic theme switching from propagating correctly.

A theme toggle button is a primary command in the CommandBar (visible, not hidden in overflow).

Critical implementation requirements for correct theme toggling:
- The root `Grid` of `MainShellView` **must** have `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"` — without this, the background remains dark when switching to light theme, making text invisible.
- All UI brushes must use `{ThemeResource ...}` bindings, not hardcoded colors.
- The `SetActive()` method on `FilePaneView` resolves border brushes dynamically via `Resources.TryGetValue` to pick up the current theme's values.

The title bar colors are synchronized with the current theme via `AppWindow.TitleBar` color properties in `MainShellWindow.ApplyTitleBarTheme()`, ensuring the title bar matches the content area in both dark and light modes.

Do not create a custom color system in v1.

## 27.6 Startup / lazy loading

The application must show the UI shell immediately on startup, then load data asynchronously.
Implementation: `OnShellViewLoaded` calls `Task.Yield()` before `InitializeAsync()`, allowing the WinUI render loop to paint the empty shell first.
Each pane displays a `ProgressRing` overlay while loading. This prevents the "white blink" artifact where the entire window appears blank until data is ready.

## 27.5 App icon

The app uses a custom icon (`Assets/app-icon.ico`) set via:
- `<ApplicationIcon>` in the `.csproj` (embeds into the EXE)
- `AppWindow.SetIcon()` at runtime (sets the window title bar icon)

## 27.4 Pane emphasis

Use borders, header tone, and focus visuals to indicate active pane.
Do not rely on only accent color changes.

---

## 28. Suggested XAML / view structure

Suggested top-level views:

- `MainWindow`
- `MainShellView`
- `FilePaneView`
- `FileListHeaderView`
- `OperationProgressDialog`
- `CopyMoveDialog`
- `DeleteConfirmationDialog`
- `PropertiesDialog`
- `FavouritesDialog`

Suggested supporting UI types:

- `FileItemRowView`
- `PaneStatusView`
- `StatusBarView`

One file per type must still be respected.

---

## 29. View model structure

Suggested UI-facing view models:

- `MainShellViewModel`
- `FilePaneViewModel`
- `FilePaneHeaderViewModel`
- `StatusBarViewModel`
- `CopyMoveDialogViewModel`
- `DeleteConfirmationDialogViewModel`
- `PropertiesDialogViewModel`
- `FavouritesDialogViewModel`
- `OperationProgressViewModel`
- `OperationResultsViewModel`

Do not place file-system logic in the view models.
They coordinate UI state and invoke command/application services.

---

## 30. UI service abstractions

The UI layer should use explicit service abstractions.

Recommended:

- `IDialogService` — implemented by `WinUiDialogService` (Presentation layer), uses `ContentDialog` and requires `XamlRoot` set after window activation
- `IClipboardService` — implemented by `WinUiClipboardService` (Presentation layer)
- `IUiDispatcher`
- `IShortcutService` if needed
- `INotificationService`
- `IFileIconService` if icons are used
- `IThemeService` only if needed

---

## 31. Recommended implementation phases

## Phase 1 — shell and pane skeleton

Build:

- main window
- two panes
- path bars
- active pane switching
- status bar
- placeholder file list

Definition of done:
- the window works
- focus and active pane are visible
- keyboard can switch panes

## Phase 2 — navigation and list rendering

Build:

- actual folder loading
- path entry
- enter/up/refresh
- columns
- row rendering
- empty/loading/error states

Definition of done:
- each pane can navigate independently
- large folder loading does not freeze the UI

## Phase 3 — selection and keyboard model

Build:

- multi-selection
- range selection
- select all
- clear selection
- incremental search
- stable cursor item behavior

Definition of done:
- pane behaves like a keyboard-capable file manager rather than a generic list

## Phase 4 — core commands

Build:

- copy
- move
- rename
- delete
- create folder
- copy full path
- properties

Definition of done:
- all commands can be launched from keyboard
- dialogs are functional
- focus returns correctly after completion

## Phase 5 — bulk workflows

Build:

- progress dialog
- result summary dialog
- error reporting
- partial-failure UX
- parallel-execution switch

Definition of done:
- long-running operations have clear user feedback

## Phase 6 — favourites and polish

Build:

- persisted favourites
- favourites dialog
- open/remove behavior
- visual polish
- accessibility cleanup
- final shortcut map

Definition of done:
- daily-use workflow is coherent and fast

---

## 32. Keyboard shortcut plan

Recommended initial shortcut map:

- `Tab` — switch active pane (focus moves to new pane's file list)
- `Ctrl+L` — focus path box in active pane
- `Enter` — open folder / navigate to parent if on `..` / no-op on files
- `Backspace` — go to parent directory
- `Ctrl+R` — refresh
- `F5` — copy
- `F6` — move
- `F2` — rename
- `F7` — create folder
- `F8` — delete
- `Alt+Enter` — properties
- `Ctrl+Shift+C` — copy full path
- `Ctrl+A` — select all
- `Space` — toggle selection and advance cursor
- `Insert` — toggle selection and advance cursor (Total Commander convention)
- `Ctrl+D` — add favourite
- `Ctrl+B` — open favourites flyout
- `Esc` — clear selection / clear incremental-search buffer
- `Home` — jump to first item
- `End` — jump to last item
- `Up` / `Down` — move cursor (native ListView)
- `Shift+Up` / `Shift+Down` — extend range selection (native ListView)
- `Ctrl+Click` — toggle item selection (native ListView)
- `Shift+Click` — range selection (native ListView)
- Typing characters — incremental filename search

Do not overload shortcuts unnecessarily in v1.

---

## 33. UI state that must persist

Persist at least:

- window size and state
- last directory per pane
- favourites
- last active pane
- optional last sort per pane
- optional last column widths

Do not persist fragile transient state such as incomplete dialogs.

---

## 34. Anti-patterns the agent must avoid

The agent must avoid:

- putting file-system operations directly in code-behind
- mixing pane state between left and right panes
- making the command bar the primary workflow
- modal blocking for folder refresh
- rebuilding the full item source on every tiny UI event
- storing selection only inside the visual control
- relying on the control's default behavior without defining app-level rules
- hardcoding all shortcuts in random event handlers
- hiding errors in logs only
- implementing only mouse-friendly flows

---

## 35. Definition of done for the UI document

The UI plan is considered implemented when:

- both panes are independently navigable
- active pane is always obvious
- every implemented command is reachable by keyboard
- large folders remain usable
- bulk operations have progress and result UX
- favourite folders are persisted and usable from keyboard
- FileId is visible in the UI
- non-NTFS paths are rejected cleanly
- dialogs restore focus correctly
- command and engine tests can run without depending on UI automation
- only a small UI smoke-test layer is needed

---

## 36. Final recommendation to the agent

If there is a trade-off between:
- perfect visual polish
- and precise keyboard semantics + stable pane state

the agent must choose:

**precise keyboard semantics + stable pane state**

That is the core value of this application.
