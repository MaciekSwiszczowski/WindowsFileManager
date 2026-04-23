# AGENT_IMPLEMENTATION_BRIEF.md

## Purpose
This document is a strict implementation brief for an autonomous coding agent. It complements the main product specification and defines the required project defaults, coding standards, architecture constraints, repository layout, package choices, and implementation rules.

The target product is an internal, Windows-only, dual-pane file manager for a small group of engineers.

## Mandatory Technology Choices

### Platform
- Target platform: **Windows only**.
- UI framework: **WinUI 3**.
- App model: desktop application built on the **Windows App SDK**.
- File system support in v1: **NTFS only**.
- Non-NTFS volumes are **out of scope** and must be ignored by the application.

### Runtime and language
- Use the **latest .NET LTS** version available at implementation time.
- For this project baseline, use **.NET 10** with a Windows TFM such as:
  - `net10.0-windows10.0.19041.0`
- Use the **latest C# language version** supported by the selected SDK.
- Enable nullable reference types.
- Enable implicit usings only if they do not make the codebase less explicit or harder to review.

### Win32 interop package
- Use the **Microsoft.Windows.CsWin32** NuGet package.
- The project must use the generated **`PInvoke`** API surface from that package for Win32 calls.
- Do not hand-maintain a large manual `NativeMethods` file if CsWin32 can generate the declarations.
- Use `NativeMethods.txt` or the current recommended CsWin32 input mechanism to explicitly list only the Win32 APIs required by the project.

### Testing
- Testing framework: **TUnit**.
- Implement integration tests for the command layer and the file system engine.
- Do not rely on heavy UI automation for routine command verification.

## Repository and Solution Structure

Create one solution containing these projects:

```text
/src
  FileManager.App.WinUI
  FileManager.Application
  FileManager.Domain
  FileManager.Infrastructure.FileSystem
  FileManager.Infrastructure.Interop
  FileManager.Infrastructure.Persistence
/tests
  FileManager.Application.Tests
  FileManager.Infrastructure.FileSystem.Tests
  FileManager.Testing
/docs
  SPEC_V1.md
  AGENT_IMPLEMENTATION_BRIEF.md
```

### Project responsibilities
- `FileManager.App.WinUI`
  - WinUI views, page/window composition, bindings, commands, dialogs, focus handling.
- `FileManager.Application`
  - Use cases, command handlers, operation planning, orchestration, result aggregation, policies.
- `FileManager.Domain`
  - Pure domain types and rules. No WinUI and no direct Win32.
- `FileManager.Infrastructure.FileSystem`
  - NTFS-aware enumeration and file operations.
- `FileManager.Infrastructure.Interop`
  - CsWin32-generated interop boundary and thin wrappers.
- `FileManager.Infrastructure.Persistence`
  - Persisted settings, favourites, user preferences.
- `FileManager.Testing`
  - Shared test fixtures, temporary directory builders, helper assertions.

## File and Type Organization Rules

### One file per type
This is mandatory.
- Each public class, record, enum, interface, struct, and exception must live in its **own file**.
- Each internal type should also live in its **own file** unless it is a tiny private nested type with a strong locality reason.
- File name must exactly match the type name.
- Partial types are allowed only when required by the framework or source generators.

Examples:
- `CopyItemsCommand.cs`
- `DeleteItemsHandler.cs`
- `DirectoryEntryViewModel.cs`
- `OperationResult.cs`
- `FavouriteFolder.cs`
- `LockedFileException.cs`

### Namespace rules
- Use clear, folder-aligned namespaces.
- Do not create overly deep or decorative namespaces.
- Keep namespaces stable and predictable.

Recommended examples:
- `FileManager.Application.Commands.CopyItems`
- `FileManager.Domain.Model`
- `FileManager.Infrastructure.FileSystem.Enumeration`
- `FileManager.App.WinUI.ViewModels`

## Coding Style Rules

### General style
- Use modern, idiomatic C#.
- Prefer small, focused types.
- Prefer composition over inheritance.
- Prefer immutable records or readonly types for data transfer and domain result objects.
- Keep methods short and intention-revealing.
- Avoid god classes.
- Avoid static mutable state.
- Avoid service locators.

### Dependency injection
- Use constructor injection.
- Register services explicitly in the app startup/composition root.
- Do not resolve services ad hoc from the container in arbitrary classes.

### Asynchrony
- Use async APIs for I/O-bound work.
- Do not block on async work with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Accept `CancellationToken` in all application-layer and file-operation APIs where cancellation matters.

### Error handling
- Do not swallow exceptions.
- Translate low-level Win32 failures into structured application/domain results where appropriate.
- Preserve error codes and source path context.
- Use explicit result objects for bulk operations instead of relying only on exceptions.

### Logging
- Use `Microsoft.Extensions.Logging` abstractions.
- Log operation start, completion, cancellation, and partial failure summary.
- Avoid noisy per-file info-level logs for successful operations unless specifically enabled.

### Comments and readability
- Prefer self-explanatory names and small methods over explanatory comments.
- Use comments sparingly and only when they add non-obvious context.
- Do not add boilerplate comments.

## UI and UX Rules

### Core layout
- Main window uses a **dual-pane** layout.
- Exactly one pane is active at a time.
- Each pane maintains:
  - current directory
  - selected items
  - focused item
  - sort state
  - filter state if introduced later

### Keyboard-first behavior
Keyboard usage is a primary requirement, not an optional enhancement.

Minimum keyboard expectations:
- `Tab` switches active pane or logical focus zones.
- Arrow keys move focus inside the active pane.
- `Enter` opens a directory.
- `Backspace` navigates to parent directory.
- `Ctrl+L` focuses path entry.
- `Ctrl+R` refreshes.
- `F5` copy.
- `F6` move.
- `F2` rename.
- `F7` create folder.
- `F8` delete.
- `Alt+Enter` view properties.
- `Ctrl+Shift+C` copy full path.
- `Ctrl+A` select all.
- `Ctrl+D` add favourite.
- `Ctrl+B` open favourites.

### Item listing
The file list must support:
- keyboard navigation
- multi-select
- large directory handling
- asynchronous refresh
- virtualization or efficient incremental rendering
- immediate folder navigation on `Enter` and double-click
- a pane-local loading progress bar that stays visible until the directory scan ends
- selecting the synthetic `..` row first when entering a non-root directory
- falling back to the nearest existing ancestor if the target directory disappears during load

### Columns
v1 file list columns should include:
- Name
- Extension
- Size
- Modified time
- Attributes
- NTFS FileId

### FileId display rule
- Display **FileId only** in the UI.
- Do not display drive serial number in the UI.
- Internal code may still retain volume context if needed for correctness, but that is an implementation detail.
- FileId must be treated as an NTFS diagnostic identifier, not a globally unique cross-volume identity.

## Domain Model Guidance

Create explicit domain/application types for the following concepts.

### Navigation
- `PaneId`
- `PaneState`
- `DirectoryLocation`
- `DirectoryEntry`
- `DirectoryEntryType`

### Selection and favourites
- `SelectionSet`
- `FavouriteFolder`
- `FavouriteFolderId`
- `FavouriteFolderCollection`

### Operations
- `FileOperationType`
- `FileOperationRequest`
- `FileOperationPlan`
- `FileOperationItem`
- `FileOperationOptions`
- `CollisionPolicy`
- `ParallelExecutionOptions`
- `FileOperationProgress`
- `FileOperationItemResult`
- `FileOperationResult`
- `FileOperationStatus`

### Errors and diagnostics
- `FileOperationError`
- `FileOperationErrorCode`
- `PathValidationResult`
- `VolumeInfo`
- `NtfsFileId`

## Required Architecture

Use a layered architecture with strict dependency direction:

```text
WinUI UI
  -> Application
    -> Domain
    -> Infrastructure.FileSystem
    -> Infrastructure.Persistence
    -> Infrastructure.Interop
```

### Dependency rules
- `Domain` depends on nothing application-specific or UI-specific.
- `Application` depends on domain abstractions and infrastructure abstractions, not concrete UI types.
- `App.WinUI` must not call raw Win32 APIs directly.
- All Win32 file system calls must go through the infrastructure layer.

### Mandatory interfaces
Define interfaces so the application layer is testable without WinUI:
- `IFileSystemService`
- `IFileOperationService`
- `IVolumeService`
- `IFavouriteFolderStore`
- `IClipboardService`
- `IPathNormalizationService`
- `IFilePropertiesService`
- `IClock` if timestamps are used in persisted data or logs

## File System and Interop Rules

### NTFS-only policy
- Enumerate available volumes.
- Accept only volumes whose file system is NTFS.
- Hide or ignore non-NTFS volumes in normal UX.
- Block direct path navigation to non-NTFS locations.

### Long path support
- The application must be long-path aware.
- Normalize paths for internal file-system operations.
- Use extended path handling where needed.
- The engine must be able to work with paths that exceed legacy `MAX_PATH` limitations.

### Win32 API usage
Use CsWin32-generated `PInvoke` bindings for required APIs, such as:
- `GetVolumeInformationW`
- `GetFileInformationByHandleEx`
- `CreateFileW`
- `CopyFile2` or the selected equivalent strategy
- `MoveFileExW`
- `DeleteFileW`
- `RemoveDirectoryW`
- `CreateDirectoryW`

Only include APIs that are actually needed.

### Reparse points and links
v1 behavior must be explicit.
- Do not accidentally traverse or destroy targets through ambiguous link handling.
- Delete operations on links must delete the link itself, not the target.
- Directory junction handling must be deliberate and tested.

## Command Definitions

Create one application command/use case type per operation.

Minimum required commands:
- `NavigateToPathCommand`
- `NavigateIntoDirectoryCommand`
- `NavigateToParentCommand`
- `RefreshDirectoryCommand`
- `CopyItemsCommand`
- `MoveItemsCommand`
- `RenameItemCommand`
- `DeleteItemsCommand`
- `CreateFolderCommand`
- `GetItemPropertiesCommand`
- `CopyItemFullPathCommand`
- `AddFavouriteFolderCommand`
- `RemoveFavouriteFolderCommand`
- `OpenFavouriteFolderCommand`

Each command must have:
- request model
- handler
- result model

## Bulk Operations Rules

### Multi-select
- Support multi-select in each pane.
- Support recursive operations for directories.
- Bulk operations must produce per-item results and a final aggregate summary.


### Overwrite and collisions
Define explicit collision behavior:
- `Ask`
- `Overwrite`
- `OverwriteAll`
- `Skip`
- `SkipAll`
- `RenameTarget`
- `RenameAll`
- `Cancel`

The command result must preserve which choice was applied.

### Partial failure semantics
Bulk operations must not collapse all detail into one generic success/failure outcome.
Each bulk operation must return:
- overall status
- per-item results
- list of failures
- list of warnings
- cancellation indicator

Possible aggregate statuses:
- `Succeeded`
- `CompletedWithWarnings`
- `CompletedWithErrors`
- `Cancelled`
- `Failed`

## Parallel Execution Rules

Parallel execution is optional and user-controlled.

### v1 rule set
- Default: **off**.
- Use only for independent operations where ordering is not semantically required.
- Limit concurrency with a configurable maximum degree of parallelism.
- Do not parallelize intra-volume rename/move if it makes semantics harder to reason about.
- Directory creation must be idempotent and safe under concurrency.
- Aggregate results deterministically.

### Configuration
Create a settings model such as:
- `ParallelExecutionEnabled`
- `MaxDegreeOfParallelism`
- `ParallelizableOperationKinds`

## Persistence Rules

Persist the following user settings:
- favourite folders
- whether parallel execution is enabled
- last used max degree of parallelism
- last known pane locations (left and right) and the last active pane;
  written on application close, restored on startup with fallback to the
  first available NTFS drive when a saved path is no longer reachable
- last window size/state if desired

Storage rules:
- use a simple structured local format such as JSON
- version the settings file
- tolerate missing or partially corrupt settings by falling back to defaults

## Modern Programming Constraints

### Preferred patterns
- records for immutable DTO-style types
- readonly structs only when justified
- pattern matching where it improves clarity
- file-scoped namespaces
- primary constructors only where they improve clarity and do not hurt readability
- collection expressions where appropriate and supported

### Avoid
- giant helper classes
- anemic utility dumping grounds
- reflection-based magic for core behavior
- hidden global state
- deeply coupled code-behind logic

## Required NuGet Packages

At minimum, evaluate and use packages like:
- `Microsoft.Windows.CsWin32`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Options`
- `TUnit`

Use only what is needed. Do not add unnecessary framework or UI abstraction packages.

## Testing Instructions for the Agent

### General test rule
Every implemented command must have integration tests in TUnit.

### Test scope
Focus on:
- application command handlers
- file-system engine behavior
- persisted settings behavior

Do not make UI automation the primary testing strategy.

### Required test fixtures
Create reusable TUnit fixtures for:
- temporary NTFS directory tree creation
- long-path scenarios
- locked-file scenarios
- read-only file scenarios
- junction/symlink scenarios where supported and safe
- multi-select/bulk-operation scenarios
- favourite folder persistence

### Minimum command test coverage
For each implemented command, test:
- happy path
- validation failure
- cancellation if supported
- partial failure if applicable
- locked/read-only path behavior if applicable
- non-NTFS rejection where relevant

### Examples
- `CopyItemsCommand` tests:
  - copy one file
  - copy many files
  - recursive directory copy
  - overwrite denied
  - overwrite accepted
  - cancellation mid-operation
  - locked destination or source behavior
- `DeleteItemsCommand` tests:
  - delete regular file
  - delete many files
  - delete link without deleting target
  - read-only file behavior
  - locked file behavior
- favourites tests:
  - add
  - remove
  - persist
  - reload
  - reject invalid or non-NTFS path

## Delivery Rules for the Agent

When implementing, the agent must:
- keep commits or changes logically grouped by feature
- avoid speculative features outside v1
- prefer correctness and testability over cleverness
- add tests together with each feature
- keep the UI thin and the application layer strong
- preserve one file per type throughout the implementation

## Explicit v1 Out of Scope
The agent must not implement these unless later instructed:
- tabs
- FTP/SFTP
- archives
- shell extensions
- recycle bin integration
- permission editor
- ADS editor
- USN journal tooling
- hashing workflows
- custom preview handlers
- plugin model
- background indexing
- search subsystem beyond simple in-pane incremental focus search

## Implementation Priority Order
1. Solution structure and project setup.
2. Core domain types.
3. Interop boundary with CsWin32.
4. NTFS volume detection and path validation.
5. Directory enumeration and pane state.
6. Basic navigation commands.
7. Basic file operations.
8. Bulk operations.
9. Favourites persistence.
10. Parallel execution toggle.
11. Properties and FileId display.
12. Final hardening and test expansion.

## Final Acceptance Criteria
The implementation is acceptable only if all of the following are true:
- Windows-only WinUI 3 application builds and runs.
- Uses latest .NET LTS baseline for the project.
- Uses CsWin32-generated `PInvoke` interop.
- Ignores non-NTFS volumes.
- Displays NTFS FileId in the UI.
- Supports the defined v1 command set.
- Supports multi-select and bulk operations.
- Supports favourite folders.
- Supports optional parallel execution.
- Handles long paths.
- Has integration tests in TUnit for all implemented commands.
- Maintains one file per type across the codebase.
