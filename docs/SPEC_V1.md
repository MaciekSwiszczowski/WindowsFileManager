# File Manager Product Specification (v1)

## Purpose
This document defines the v1 product specification for an internal Windows-only dual-pane file manager intended for a small group of engineers.

The application is inspired by Total Commander in workflow, but the initial version must stay intentionally small, predictable, keyboard-first, and easy to test.

The product target is:
- Windows desktop only
- NTFS only
- WinUI 3 UI
- long-path capable
- prepared for future low-level NTFS features
- implemented with a strict separation between UI, command layer, and file-system engine

---

## Product Goals

### Primary goals
- Fast, keyboard-first navigation and file operations.
- Deterministic, testable command behavior.
- Safe handling of bulk operations.
- Clear semantics for cancellation, partial failure, and locked files.
- Solid architectural base for future low-level file operations.

### Non-goals for v1
- Explorer shell replacement
- plug-in system
- archive support
- FTP/SFTP/cloud providers
- permissions editor
- advanced NTFS tools such as USN journal browsing, ADS editing, ACL editing, hard link tools, reparse-point authoring
- shell extensions and Explorer context-menu integration
- right-click menu

---

## Platform and Scope Constraints

### Platform
- Windows only
- Desktop application only
- WinUI 3
- x64 only in v1

### File system scope
- NTFS only
- Non-NTFS volumes must be ignored completely by the application
- The application must not attempt to expose or operate on FAT, exFAT, ReFS, network shares that do not behave as NTFS, optical media, or virtual file providers in v1
- The application does not support special files like links or sparce files in v1

### Path handling
- Long paths must be supported from day one
- Internal file-system operations must be designed so that later low-level NTFS work can be added without redesigning the engine

---

## User Model

The user is a technically proficient Windows engineer.
The application is optimized for:
- keyboard usage
- repeatable workflows
- working with many files quickly
- confidence in operation results
- dual panel work

The application is not optimized for novice users in v1.

---

## High-Level UX

### Layout
The main window contains:
- left file panel
- right file panel
- right-side inspector panel (visible by default, toggleable)
- exactly one active panel at any given time
- command area or status area
- modal operation progress dialog shown only while long-running operations are active

### Active panel rules
- Navigation commands apply to the active panel
- Selection belongs to the active panel
- Copy and move default to the inactive panel as destination
- Visual distinction between active and inactive panel must be obvious

### Keyboard-first requirement
Every v1 feature must be usable without a mouse.

Required keyboard capabilities:
- switch active panel
- move selection up and down
- open directory
- go to parent directory
- refresh
- multi-select
- trigger all file commands
- open favourites
- add current path to favourites
- confirm or cancel dialogs from keyboard

Mouse support is allowed, but it is secondary.

---

## Functional Scope (v1)

### 1. Navigation
The application must support:
- open selected directory
- move to parent directory
- navigate to a typed absolute path
- refresh current panel
- if the current directory disappears externally, refresh/watch invalidation rolls the pane back to the highest existing ancestor directory
- switch active panel
- preserve per-panel navigation state independently

Optional but recommended in v1:
- remember last path per panel on application restart

### 2. Directory Listing
Each panel must display at minimum:
- name
- extension
- type indicator if useful
- size
- last write time
- attributes

Sorting support in v1:
- name
- extension
- size
- last write time

A directory entry must be visually distinguishable from a file entry.

### 3. Inspector And NTFS FileId Display
The file list must stay on cheap metadata only. NTFS `FileId` is shown in the inspector for a single selected item.

Rules:
- Do not display volume serial number in the UI.
- Display `FileId` in a stable hexadecimal format.
- Treat `FileId` as diagnostic metadata in the UI.
- The UI contract does not promise global uniqueness across all volumes.
- `FileId` must not be collected in the hot path during folder enumeration.
- `FileId` loading in inspector is deferred (`200 ms` debounce), cancellable, and resilient to failures/timeouts.

Internal note:
- The engine may keep additional identity context internally if needed, but the inspector UI exposes `FileId` only.

### 4. Basic File Commands
The following commands are required in v1:
- copy
- move
- rename
- delete
- create folder
- copy full path
- toggle inspector

Each command must exist as a testable application/command-layer action.

### 5. Bulk Operations
The application must support bulk operations for:
- copy
- move
- delete

Bulk behavior requirements:
- multi-select
- recursive folder processing
- progress reporting
- cancellation
- error aggregation
- final summary

### 5.1 Incremental Directory Loading
Folder enumeration must be incremental and batched.

Required behavior:
- first batch appears quickly after navigation
- subsequent batches continue until enumeration completes
- enumeration is cancellable
- stale results must not appear after navigation away
- panes are visibly busy and non-interactive while loading
- `Ctrl+A` and item-selection actions do nothing while pane loading is active


### 6. Favourite Folders
The application must support favourite folders.

Required features:
- add current panel path to favourites
- remove favourite
- open favourite into active panel
- keyboard access to favourites list
- persisted storage across sessions

### 7. Inspector View
An inspector pane must be available on the right side for selected-item details.

Minimum displayed fields:
- name
- full path
- type (file or directory)
- extension
- size
- timestamps
- attributes
- NTFS `FileId`

Selection behavior:
- when no item is selected, inspector clears
- for multi-selection, inspector shows selected count only
- when exactly one item is selected, inspector shows full details

---

## NTFS-Only Policy

The NTFS-only policy is strict.

### Required behavior
- Enumerate candidate volumes and include only NTFS volumes
- Block manual path entry if target path is not on NTFS
- Block favourite creation for non-NTFS locations
- Block command execution if either source or destination is non-NTFS

### User feedback
When blocked, the user must receive a short explicit reason:
- source is not on NTFS
- destination is not on NTFS
- path is unsupported in v1

The application must not silently fall back to unsupported behavior.

---

## Command Semantics

### General command contract
Every command must return a structured result, not only throw exceptions.

Minimum result model:
- command status
- item-level successes
- item-level failures
- warnings
- cancellation state
- summary message

Suggested status values:
- Succeeded
- CompletedWithErrors
- Cancelled
- FailedValidation

### Copy
Copy must support:
- file to directory
- directory to directory recursively
- bulk copy
- progress reporting
- cancellation

### Move
Move must support:
- intra-volume move
- cross-volume move
- bulk move
- progress reporting
- cancellation

### Rename
Rename must support:
- single-item rename
- validation of invalid names
- name collision detection
- batch rename for multiple selected items

### Delete
Delete must support:
- file delete
- recursive directory delete
- bulk delete
- reporting for locked files
- reporting for read-only failures if not automatically handled

Decision for v1:
- permanent delete only
- recycle bin integration is out of scope

### Create Folder
Create folder must support:
- create in active panel current directory
- validation against invalid name
- name collision handling
- long path handling

### Copy Full Path
Copy full path must support:
- selected file path
- selected directory path
- multiple selected items copied as newline-separated list, or single-item only if product chooses that path

The chosen behavior must be explicit and tested.

---

## Cancellation Rules

Cancellation must be cooperative and observable.

Rules:
- A user can cancel a running bulk operation
- Already completed items remain completed
- Not-yet-started items must not begin after cancellation is observed
- In-progress item handling must be consistent and documented
- Final result must distinguish cancellation from normal completion

The UI must not report cancellation as success.

---

## Partial Failure Rules

Bulk operations must support partial success.

Examples:
- 97 items copied successfully, 3 items failed
- cross-volume move copied all items but failed to delete 2 sources
- delete succeeded for most items, but failed for locked files

Required output:
- aggregate status
- itemized failures
- itemized warnings
- final summary counts

The application must not collapse partial failure into a single opaque message.

---

## Locked Files and Access Errors

The engine must explicitly handle common operational failures.

Examples:
- file is locked by another process
- access denied
- sharing violation
- destination exists
- invalid file name
- path too long for an unnormalized path input
- source missing during execution because the file changed externally

The application must:
- preserve the specific reason where available
- report per-item failures in bulk operations
- avoid crashing the process for expected file-system failures

---

## Long Path Support

Long path support is mandatory.

Requirements:
- application must be long-path aware
- engine must normalize internal paths appropriately
- commands must work for paths beyond legacy MAX_PATH where supported by the OS and configuration
- tests must include long-path scenarios

The UI may display user-friendly paths, but engine operations must use the normalized internal path strategy.

---

## Architecture

A strict layered architecture is required.

### Layers
1. UI Layer
2. Command/Application Layer
3. File-System Engine Layer
4. Win32 Interop Layer

### Layer responsibilities

#### UI Layer
Responsible for:
- WinUI 3 views
- panel presentation
- keyboard routing
- binding to view models
- dialogs and progress surfaces

Progress-surface rule:
- copy/move/delete progress is presented through the dialog service as a modal progress dialog
- final operation summary is presented through the existing operation result dialog
- progress must not be implemented as a permanently allocated row/surface in the main shell layout

Must not contain file-system mutation logic.

#### Command/Application Layer
Responsible for:
- command orchestration
- validation
- conflict policy handling
- cancellation orchestration
- result aggregation
- mapping engine errors to UI-facing results

Must be independently integration-testable without UI.

#### File-System Engine Layer
Responsible for:
- NTFS-only validation
- enumeration
- metadata retrieval
- FileId retrieval
- copy/move/delete/rename/create-folder execution
- path normalization

#### Win32 Interop Layer
Responsible for:
- generated or declared Win32 bindings
- encapsulated low-level API calls
- minimal surface exposed upward

---

## Domain Model Expectations

The exact implementation may vary, but the system must have explicit models for at least the following concepts:

- PanelState
- FileSystemEntry
- FavouriteFolder
- FileIdentity
- CommandRequest
- CommandResult
- ItemOperationResult
- OperationProgress
- OperationSummary
- ErrorDescriptor

### FileSystemEntry minimum data
- full path
- display name
- entry type
- size
- timestamps
- attributes
- selection state if held in UI/view-model layer

### FileIdentity
UI requirement:
- `FileId` is displayed in inspector only (not in pane columns)

Engine requirement:
- enough information to operate safely and consistently

---

## Performance and Responsiveness Requirements

### UI responsiveness
- The UI must remain responsive during enumeration and operations
- Long operations must be asynchronous from the UI perspective
- The application must not block the UI thread during bulk file operations

### Directory loading
- Large directories must be handled with virtualization or equivalent efficiency strategy
- Initial render should prioritize visible items
- Sorting should be efficient enough for practical engineering use

---

## Persistence Requirements

Persist at minimum:
- favourites
- per-user settings
- operation preferences where appropriate
- last visited path for each panel (left and right)
- last active panel (left or right)

Settings must be stored in a local, user-scoped, versionable format.

### Session state persistence

- The last visited path for each pane and the last active pane are persisted
  between sessions.
- Writes happen once on application close (via the window `Closing` event),
  not on every navigation.
- On startup, if a persisted path cannot be opened (drive unplugged, folder
  deleted, permission denied), the pane falls back to the first available
  NTFS drive root. Initialization never fails because of a stale saved path.

### Startup behavior

- The main window must be shown and become interactive before any file
  system I/O runs.
- Drive enumeration and initial pane navigation happen in the background,
  in parallel for left and right panes, after the UI has been presented.
- Each pane renders its contents as soon as its own background load
  completes; one pane must not block the other.

---

## Logging and Diagnostics

The application must have structured logging.

Minimum logging requirements:
- application start and stop
- command start and completion
- aggregate operation outcome
- item-level failure details for bulk operations
- unexpected exceptions

Recommended:
- operation correlation id
- duration metrics

Logs must be useful for diagnosing customer-like internal issues.

---

## Security and Elevation

v1 should run unelevated by default.

Rules:
- do not require administrator privileges for normal use
- access-denied scenarios must fail clearly
- optional future "retry elevated" behavior may be added later but is not required for v1

---

## Testing Strategy

### General test policy
All implemented commands must have integration tests in TUnit.

The emphasis is:
- engine tests
- command-layer integration tests
- minimal UI smoke tests only if needed

The system must not rely on UI automation for the main correctness guarantee.

### Required integration test areas

#### Navigation
- open directory
- parent directory
- navigate to explicit path
- reject non-NTFS path
- refresh after external changes
- refresh when the current directory was deleted externally and the pane must fall back to the highest existing ancestor

#### Copy
- single file copy
- directory copy
- bulk copy
- cancellation
- locked file handling
- long path copy

#### Move
- intra-volume move
- cross-volume move semantics if supported in test environment
- partial failure during delete stage
- cancellation

#### Rename
- single rename
- invalid rename
- collision handling
- batch rename preview and apply

#### Delete
- single file delete
- recursive folder delete
- bulk delete
- locked file failure
- read-only behavior according to chosen policy

#### Create Folder
- normal creation
- collision handling
- invalid name
- long path creation

#### Inspector
- file metadata retrieval
- directory metadata retrieval
- deferred `FileId` retrieval for single-item inspector selection
- inspector clears for empty selection and shows selected count for multi-selection

#### Copy Full Path
- single item
- multiple selection behavior according to chosen product rule

#### Favourites
- add
- remove
- persistence round-trip
- keyboard-accessible command path if modeled at command layer
- reject non-NTFS favourite

### Test environment requirements
- tests must run on Windows
- tests must use real NTFS temporary locations
- tests must isolate state per test or fixture
- tests must clean up deterministically
- tests that require locked files must create real lock conditions

### UI test scope
Do not use UI Automation.
Do not test the UI in v1.

---

## Out of Scope for v1

The following are explicitly out of scope:
- Recycle Bin
- Undo/redo for file operations
- Explorer shell integration
- custom context-menu extensions
- right-click context menu
- tabs
- search engine across volumes
- content preview pane
- thumbnail system
- file content diff tools
- permissions editor
- ADS editor
- symlink/junction creation tools
- hard-link management
- network and cloud providers
- archive browsing
- drag and drop between app and Explorer

---

## Acceptance Criteria

v1 is acceptable when all of the following are true:
- The application runs on Windows as a WinUI 3 desktop app
- It shows two panels with independent navigation state
- It shows a right-side inspector pane that is visible by default
- It ignores non-NTFS volumes completely
- It does not display `FileId` in pane columns
- It displays NTFS `FileId` in the inspector for a single selected item
- All listed commands work from keyboard
- Bulk operations are supported with progress, cancellation, and aggregated error reporting
- Favourites are persisted and keyboard-accessible
- Long path scenarios are supported
- All implemented commands have TUnit integration coverage at engine and command-layer level
- The codebase preserves a strict separation between UI, command layer, engine, and interop

---

## Implementation Guidance for Agent

The implementing agent must treat this as a constrained v1 product, not as an invitation to add features.

Priority order:
1. architecture skeleton
2. NTFS-only validation
3. dual-panel navigation
4. directory listing with cheap metadata only
5. inspector with deferred selected-item `FileId` loading
6. single-item commands
7. bulk operations
8. favourites
9. final hardening and test completion

Any feature not listed as in-scope must not be added unless explicitly requested.
