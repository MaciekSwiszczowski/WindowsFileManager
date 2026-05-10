# Spec: Low-Hanging Improvements (Code Quality, Stability, Memory)

## Purpose

This is the active near-term backlog. It replaces items archived from `archive/SPEC_BUG_FIXES.md`, `archive/SPEC_RENAME_BUGS.md`, `archive/SPEC_PERF_LOW_HANGING_FRUIT.md`, `archive/SPEC_NATIVE_MODERNIZATION.md`, and most of `SPEC_FEATURE_LOW_HANGING_FRUIT.md` after the table/inspector rework. Each entry is a small, independently-shippable work unit with named files and a clear "done" condition.

The next major feature work in line is **Copy / Move / Delete with cancellation**, followed by **rich error diagnostics**. Several entries below are prerequisites for that work; they are flagged.

The author prefers single-responsibility classes in the **200–300-line** range. Several files exceed that today (§3 lists them).

## Permanent non-goals

- **Drag-and-drop will never be supported.** Past experience showed too many edge cases (cross-process drop targets, partial transfers, focus loss). Anything that would naturally introduce drag-and-drop (e.g. dropping into Explorer, accepting drops from external apps) is out of scope. Use shortcuts and the dialog-driven copy/move flows instead.

---

## 1. Active gaps (rewire what was dropped)

These gaps exist in the current code as a direct consequence of the table rework: the new `SpecFileEntryTableView` ships the keyboard input path (key-pressed messages, selection messages), but the consumer side that turned those messages into actual file operations was the old `FilePaneViewModel`, which is gone.

### I-1. Bring up the command Coordinator

**Why.** `KeyboardManager` already publishes `CopyKeyPressedMessage`, `MoveKeyPressedMessage`, `DeleteKeyPressedMessage`, `CreateFolderKeyPressedMessage`, `CopyPathKeyPressedMessage`, `PropertiesKeyPressedMessage`, `RenameKeyPressedMessage`, `NavigateUpKeyPressedMessage`, `ActivateInvokedMessage`. Nothing consumes them except `RenameService` (rename only) and `MessageLogStore` (logging only). Pressing F5/F6/F7/F8 today is a no-op.

The contract for the Coordinator is already specified in `SPEC_FILE_ENTRY_TABLE_VIEW.md` §14 (state, subscriptions, resolution rules, resolved messages). It has not been implemented.

**Where.** New file: `src/WinUiFileManager.Presentation/Services/FileTableCommandCoordinator.cs` (or in `Application/` if it stays UI-agnostic — the spec keeps it application-scoped, no UI types). Register as singleton in `ServiceConfiguration`.

**What it does.** Subscribes to the eight `*KeyPressedMessage`s, the four `FileTable*Message`s (`Focused`, `SelectionChanged`, `NavigateUpRequested`, `NavigateDownRequested`), and `ActivateInvokedMessage`. Holds `_activeIdentity` + `_selectionByIdentity`. Publishes the eight resolved domain messages (`CopyRequestedMessage`, `MoveRequestedMessage`, `DeleteRequestedMessage`, etc.) per `SPEC_FILE_ENTRY_TABLE_VIEW.md` §14.5.

**Done when.**
- Coordinator is registered and constructed at app start.
- F5 / F6 / F8 / F7 / Alt+Enter / Ctrl+Shift+C / Backspace / Alt+Up emit the matching `*RequestedMessage` with the active selection.
- Empty-selection cases match the §14.5 "no-op" rules.
- One unit test per resolution rule, using a dedicated `StrongReferenceMessenger` instance (inject `new StrongReferenceMessenger()`, not the process default) and a fake `SpecFileEntryViewModel`.

### I-2. FileOperationDialogService (consumer for the resolved messages)

**Why.** Without a consumer for `CopyRequestedMessage`, `MoveRequestedMessage`, `DeleteRequestedMessage`, `CreateFolderRequestedMessage`, the Coordinator alone changes nothing. The dialog service is responsible for confirming (delete), capturing the destination (copy/move), validating the name (create folder, rename), then delegating to the existing handlers in `Application/FileOperations`.

**Where.** New file: `src/WinUiFileManager.Presentation/Services/FileOperationDialogService.cs`. Use the existing `ShowDialogMessage` + `DialogTemplates.xaml` infrastructure (see `RenameService.cs` for the pattern). Add new template keys + dialog view-models for Copy, Move, Delete confirm, Create Folder.

**Cancellation.** Each handler call must run with the `OperationProgressViewModel.CancellationToken`. The progress VM (already exists at `ViewModels/OperationProgressViewModel.cs`) needs to be shown as a modal `ContentDialog` while the operation runs and reset on completion. A press of the dialog's Cancel button calls `_cts.Cancel()`; the handler returns an `OperationSummary` with `Status = Cancelled`.

**Destination resolution.** Copy/Move need the *other* pane's path. Pull from `MainShellViewModel` via a small abstraction (`IDestinationPathProvider` with one method `GetDestinationPath(string sourceIdentity)`). Today the *current* path of each pane only lives inside `FileEntryTableDataSource.States.CurrentPath`; expose it via `ActivePanelsService` or a new `PanelPathsService`.

**Concurrency.** `DialogService` already serializes with a `SemaphoreSlim`; `FileOperationDialogService` queues new requests while one is open.

**Done when.**
- F5 with a selection and the other pane on a writable folder copies the selection there.
- F6 moves with the same flow.
- F8 / Delete prompts confirmation, then deletes.
- F7 / Ctrl+Shift+N prompts for a folder name and creates it.
- A long copy can be cancelled mid-operation via the progress dialog.
- All four flows have integration tests in `Application.Tests` that exercise the coordinator + dialog plumbing using a fake `IDialogService`.

### I-3. Drop the hardcoded `C:\FileEntryTableTest\Left/Right` paths

**Why.** `MainShellView.xaml.cs:75-86` hardcodes the initial path for both data sources. The persistence flow does load `LastLeftPanePath` / `LastRightPanePath` into `MainShellViewModel`, but those values are never threaded back into `FileEntryTableDataSource`.

**Fix.** `EnsureFileEntryDataSources` should pull initial paths from `_currentSettings.LastLeftPanePath` / `LastRightPanePath` (with fallback to the first available NTFS drive — there is already a `WindowsShellService.GetVolumes` path through Infrastructure).

**Done when.**
- App restarts on the same paths used last session.
- A deleted-since-shutdown saved path falls back to the highest existing ancestor (per `SPEC_V1.md` §1).
- A non-NTFS saved path falls back to the first NTFS drive.
- Test in `MainShellViewModel.Tests` covers the three fallback cases.

### I-4. Consumer for `Ctrl+R` / refresh

**Why.** `KeyboardManager` does not emit a refresh message today; `MainShellViewModel.RefreshActivePaneAsync` is a stub. The new architecture has `FileEntryTableDataSource` that owns directory state, so refresh is a one-method call on the active data source.

**Where.** Add `RefreshKeyPressedMessage` (Application/Messages), wire `Ctrl+R` in `KeyboardManager`, expose a refresh method on `FileEntryTableDataSource`, call it from the Coordinator (or directly from a small `RefreshService` parallel to `RenameService`).

**Done when.** Ctrl+R triggers a re-enumeration of the active pane.

### I-5. Consumer for properties (Alt+Enter), copy-path (Ctrl+Shift+C), navigate-up

**Why.** `Alt+Enter` already emits `PropertiesKeyPressedMessage`; the old behavior called the shell `SHObjectProperties` adapter. `Ctrl+Shift+C` emits `CopyPathKeyPressedMessage` — no consumer. `Backspace`/`Alt+Up` emit `NavigateUpKeyPressedMessage` — no consumer.

**Where.**
- Properties: small consumer that calls `WindowsShellService.ShowPropertiesAsync(path)`. The infrastructure is intact.
- Copy path: small consumer using the existing `WinUiClipboardService`. Multi-selection joins paths with newlines (per `SPEC_V1.md` §Copy Full Path).
- Navigate up: a small consumer that drives the active `FileEntryTableDataSource` to its parent path.

**Done when.** Each of the three shortcuts performs the documented action.

---

## 2. Rich error diagnostics

This is the user-facing differentiator the app cares about: when an operation fails, a Windows engineer wants to know **why** at a Sysinternals-level of detail, not the generic "Access denied" string.

### D-1. Error model expansion

**Where.** `src/WinUiFileManager.Domain/Errors/OperationError.cs`, `src/WinUiFileManager.Domain/Enums/FileOperationErrorCode.cs`.

The current `OperationError` carries `Path`, `Code` (a 13-value enum), `Message`, `NativeErrorCode`. Extend with a structured **`OperationErrorContext`** that the UI can render category by category:

```csharp
public sealed record OperationErrorContext
{
    public FileLockDiagnostics? LockedBy { get; init; }       // Restart Manager: which processes / services
    public DiskSpaceDiagnostics? DiskSpace { get; init; }     // free vs. quota vs. file size
    public SharingViolationDiagnostics? SharingViolation { get; init; }  // which handle / share mode
    public PathDiagnostics? Path { get; init; }               // length class, contains reparse, NTFS-or-not, link target
    public PermissionsDiagnostics? Permissions { get; init; } // ACL summary, owner, integrity level
    public AntivirusDiagnostics? Antivirus { get; init; }     // ETW or known AV vendor heuristic; best-effort
}
```

Each category's diagnostic record is a value-object in `Domain/ValueObjects/`. Each category is **best-effort**: if the lookup fails or is too expensive at the moment of failure, the field stays `null` and the UI hides that section.

**Done when.**
- New error record with at least the lock + disk-space + path categories.
- `WindowsFileOperationService` populates the context on failure paths.
- Existing tests still pass (the additions are nullable).

### D-2. Locked-file diagnostics: who is the locker

**Why.** "File is in use" without saying *who* is useless. The Restart Manager interop is already wired (`IRestartManagerInterop`, `WindowsFileIdentityService.GetLockDiagnosticsAsync`) and surfaces `FileLockDiagnostics` (PIDs, process names, services). Currently used only by the Inspector's Locks category.

**Fix.** When `WindowsFileOperationService` catches a sharing violation / `ERROR_SHARING_VIOLATION` / `ERROR_LOCK_VIOLATION`, also call `IFileIdentityService.GetLockDiagnosticsAsync(path)` and attach the result to `OperationError.Context.LockedBy`.

**UI.** A failure dialog renders a section like:

```
File is in use:
  • notepad.exe (PID 12048)
  • Microsoft.SearchProtocolHost (service)
```

**Done when.**
- A test with a deliberately-locked file via `BlockingFileOperationInterop` verifies `LockedBy` is populated.
- The lock query is bounded by a 500 ms timeout; on timeout the field stays `null`.

### D-3. Disk-space-vs-quota diagnostics

**Why.** "Not enough disk space" can mean three different things on a Windows engineer's machine: (a) volume free space below file size; (b) per-user NTFS quota hit; (c) compression / reparse cap. Today the app would just show `IoError`.

**Fix.**
1. Extend `Domain/ValueObjects/VolumeInfo.cs` to also carry `TotalBytes`, `FreeBytes`, `FreeBytesAvailable` (the Win32 `GetDiskFreeSpaceEx` distinguishes the latter from the user's available quota).
2. Add `IVolumeService.GetUserQuotaAsync(volume, sid)` for the quota-side query (`GetDiskFreeSpaceEx` distinguishes user-available from total-free; deeper quota detail uses `FSCTL_QUERY_USN_JOURNAL` or `IDiskQuotaUser` — start with the cheap one).
3. On `ERROR_DISK_FULL` / `ERROR_HANDLE_DISK_FULL`, populate `OperationError.Context.DiskSpace` with: required size, total free, user-available free, quota threshold (if known).

**UI.** Renders the most actionable line: "Required 4.2 GB; volume has 9.1 GB free but your quota leaves 1.3 GB."

**Done when.** Test in `Infrastructure.Tests/FileOperationServiceTests` injects an interop that returns `ERROR_DISK_FULL`; assertion verifies the context fields.

### D-4. Sharing-violation diagnostics

**Why.** Sharing violations (`ERROR_SHARING_VIOLATION` / 32) are different from "file in use" locks. The handle exists with a share mask that excludes the requested operation. Knowing the share mask + the holder (when available) is the actionable detail.

**Fix.** Where Restart Manager doesn't surface a process owner (some sharing scenarios are kernel-side, e.g. memory-mapped sections), fall back to enumerating handles via `NtQuerySystemInformation(SystemHandleInformation)` and matching the file id. This is heavy and admin-only; gate it behind a "deep diagnostics" toggle and log a warning if the user is not elevated.

**Done when.** The deep-diagnostics path is *behind a setting*, off by default. Documented in the code comment that admin elevation is required and the public path stays cheap.

### D-5. Path-class diagnostics

**Why.** Errors on long paths and reparse points produce confusing messages today. `PathLength` is already designed in `SPEC_LONG_PATHS.md` §2. Wire the categorization at the error site.

**Fix.** When a path-related error fires, attach `PathDiagnostics` with `PathLength` (Standard / Extended / OutOfRange), `IsReparse`, `IsSymlink`, and `IsNtfs`. The UI uses these to suggest "the long-paths toggle is off" or "this is a junction; the operation would affect the target — refuse."

**Done when.** Error context for any IO error includes `PathDiagnostics`.

### D-6. Error display surface

**Where.** New file: `src/WinUiFileManager.Presentation/Dialogs/OperationErrorDialogViewModel.cs` + a `DialogTemplates.xaml` template key.

A small `ContentDialog` showing the error code, message, and one expander per non-null context section. **No nested control trees**; reuse the inspector's two-column layout pattern (key + value, value column stretches) so it looks consistent.

**Done when.** A staged failure (file in use + path is long) renders both sections, expandable.

---

## 3. Single-responsibility refactors

Files currently over the 200–300-line ceiling, with a recommended split.

### S-1. `MainShellView.xaml.cs` (501L) → ~5 files of 80–150L

**Today.** Mixes: data-source lifecycle, splitter pointer hooks, status bar updates, key-down dispatch, theme application, item-collection-changed echo, ApplePane/RightPane management, focus management, initial-path resolution.

**Split.**
- `MainShellView.xaml.cs` (≤ 120L): just `InitializeComponent`, `Initialize(viewModel)`, `Loaded` / `Unloaded`, and forwarding to the helpers below.
- `Views/MainShellSplitterDragController.cs`: pointer-pressed/released handlers and the freeze/release logic (~80L).
- `Views/MainShellStatusBarController.cs`: status bar + items-collection echo (~80L).
- `Views/MainShellKeyboardController.cs`: `OnPreviewKeyDown`, `IsModifierDown`, `IsTextInputFocused`, `FocusActiveTable` (~120L). Stays separate from `KeyboardManager` because it covers shell-only shortcuts that don't fit the message bus (Tab pane switch, Ctrl+I inspector, Ctrl+L path focus, Ctrl+D favourites).
- `Views/MainShellDataSourceHost.cs`: `EnsureFileEntryDataSources`, `ResolveInitialPath`, the two `Apply*State` echos (~120L). Owns the two `FileEntryTableDataSource` instances. Once I-3 lands this also threads in persisted paths.

The view stays the composition root; the controllers are POCOs constructed in `Initialize`.

**Done when.** No single file in the rebuilt set exceeds 200L. Behavior unchanged. No new tests required (UI behavior, can't easily unit-test the controllers — but they become unit-testable in isolation later).

### S-2. `NtfsFileIdentityService.cs` (927L) → 9 category files + a small router

**Today.** One class implementing `IFileIdentityService`, with a method per Inspector category: identity, NTFS metadata, NTFS attribute set, cloud, links, streams, security, thumbnails, locks. The methods do not share much state — each opens its own handle, calls 1–3 interop adapters, and projects the result into a `Domain/ValueObjects/File*Diagnostics*` record.

**Split.** `Infrastructure/FileSystem/Identity/`:
- `NtfsFileIdentityRouter.cs` (~80L): implements `IFileIdentityService`, holds the nine sub-services, delegates each method.
- One file per category: `IdentityDiagnosticsProvider.cs`, `NtfsMetadataDiagnosticsProvider.cs`, `CloudDiagnosticsProvider.cs`, `LinkDiagnosticsProvider.cs`, `StreamDiagnosticsProvider.cs`, `SecurityDiagnosticsProvider.cs`, `ThumbnailDiagnosticsProvider.cs`, `LockDiagnosticsProvider.cs`. Each is 80–150L, owns its interop adapter set, accepts a `CancellationToken`, has its own integration tests.

This also satisfies M-2 from the archived `SPEC_NATIVE_MODERNIZATION.md` (cancellation correctness — easier to enforce per category file).

**Done when.** No single category file > 200L. Existing `Infrastructure.Tests/NtfsFileIdentityServiceTests` is unchanged and passes against the router.

### S-3. `FileInspectorViewModel.cs` (442L) → orchestrator + helpers

**Today.** Holds the state machine for selection → category load → field update + the deferred batch plan + the field-state cache + the cancellation-and-debounce logic + the property-changed dispatch. Some helpers are already extracted (`FileInspectorDeferredLoader`, `FileInspectorDeferredBatchPlan`, `FileInspectorModelBuilder`, `FileInspectorThumbnailMaterializer`); the orchestrator is still long.

The user's stated approach is "review the Inspector view-model method by method." That review should preserve this structure:
- Public API + selection lifecycle in `FileInspectorViewModel.cs` (target ≤ 200L).
- Category visibility / sort logic in a new `FileInspectorCategoryRegistry.cs`.
- Field-update reconciliation (the in-place row update logic from `Refresh`/`UpdateFromBatchResult`) in `FileInspectorFieldReconciler.cs`.

**Done when.** `FileInspectorViewModel.cs` ≤ 200L; behavior unchanged; existing tests pass.

### S-4. `WindowsFileOperationService.cs` (406L)

**Today.** Plan execution, parallelism, progress reporting, error mapping, cancellation — all in one file. Once §2 (rich diagnostics) lands the file will get bigger, not smaller, unless split first.

**Split.**
- `WindowsFileOperationService.cs` (≤ 180L): orchestration only — `ExecuteAsync(plan)` + dispatch + summary aggregation.
- `Execution/PlanItemExecutor.cs` (≤ 150L): the single-item execution loop (copy / move / delete) + progress reporting per item.
- `Execution/OperationErrorMapper.cs` (≤ 120L): maps `Win32Exception` / `IOException` / HResults to `FileOperationErrorCode` *and* (after §2) populates `OperationErrorContext` by calling the diagnostic providers.

**Done when.** No file > 200L; existing 462L of test coverage passes.

### S-5. `FileEntryTableDataSource.cs` (381L)

**Today.** Owns: enumeration scheduling, directory watcher subscription, the parent-row synthesis, the source cache, the public `States` `BehaviorSubject`, the cancellation tokens for the active load.

**Split.**
- `FileEntryTableDataSource.cs` (≤ 180L): the public surface (`Identity`, `Items`, `States`, `NavigateTo(path)`, `Refresh()`, `Dispose`).
- `Data/DirectoryLoadCoordinator.cs` (~120L): the load-version + cancel-active-load + load-completed pipeline.
- `Data/ParentEntryProjection.cs` (~80L): synthesis of the synthetic `..` row + parent-visibility messaging.

**Done when.** No file > 200L.

### S-6. `DialogService.cs` (323L)

**Today.** Holds the dispatcher gate, the active-dialog tracking, the `XamlRoot` attachment, the message-bus integration, and the dialog-button conversion + result projection.

**Split.**
- `DialogService.cs` (≤ 180L): the message-bus subscriptions + the open/close orchestration.
- `Dialogs/DialogPresenter.cs` (~120L): the actual `ContentDialog` construction, button mapping, dispatcher-aware `ShowAsync`. No knowledge of the messenger.

**Done when.** No file > 200L; behavior unchanged.

### S-7. `FileEntryTableBehaviorHelper.cs` (273L)

Sits on the borderline. Audit during the next table touch; if it grows past 300L, split out the parent-row pinning logic and the column-header probing logic into dedicated helpers.

---

## 4. Stability

### St-1. Disposal audit — `StrongReferenceMessenger` registration lifetimes

**Where.** `StrongReferenceMessenger` keeps registered recipients alive until they unregister. Any type that calls `IMessenger.Register<T>(this, ...)` without a matching `UnregisterAll` (or `Unregister`) in teardown will keep handlers and objects alive for the messenger’s lifetime. Recipients should be `IDisposable` and call `UnregisterAll` in `Dispose` wherever practical. Today: `RenameService` (handled), `DialogService` (handled), `MainShellView` (handled — `Unloaded` calls `UnregisterAll`), `MessageLogStore` (registers but never unregisters — process-lifetime singleton, OK), `FileEntryTableDataSource` (no messenger registration today, OK).

**Action.** Add an analyzer or a lightweight test that lists every `Register<` call and asserts the recipient is also `IDisposable` *and* that `Dispose` calls `UnregisterAll`. This is mechanical; do it once and the rule survives future additions.

### St-2. `_disposed` gate audit

Every `IDisposable` should have an idempotent `Dispose`. Today `MainShellViewModel.Dispose()` is empty — confirm by audit that nothing it owns leaks (favourites collection is safe; the inspector view-model is a transient resolved per-shell, must dispose). Today `Inspector` is owned but never disposed.

**Done when.** A small test resolves and disposes `MainShellViewModel` 100×; `StrongReferenceMessenger.Default.IsRegistered` reports zero residual recipients of any type.

### St-3. App-window closing order

`MainShellWindow.OnAppWindowClosing` cancels the close, persists state, then closes. If `PersistStateAsync` throws, `_statePersisted` is set true and the window closes anyway, swallowing the error. Add structured logging on throw and consider letting subsequent close attempts retry the persistence (e.g. only set `_statePersisted = true` after the await succeeds).

### St-4. Cancellation contract — re-throw `OperationCanceledException`

This is the surviving idea from the archived `SPEC_NATIVE_MODERNIZATION.md` M-2. Audit `NtfsFileIdentityService.*Async` and the new `WindowsFileOperationService` flow: every `catch (Exception)` must `catch (OperationCanceledException) { throw; }` first, otherwise a cancelled operation paints fallback data as if successful. With the S-2 split, this is enforceable per-category file via a copy-paste-once guard.

### St-5. Watcher restart hardening

`WindowsDirectoryChangeStream.OnError` (Infrastructure/FileSystem) restarts the watcher immediately on error. Add exponential back-off (500 ms → 30 s, capped) and a 10-failure budget after which the watcher gives up and reports `Invalidated`. This was B4 from the archived bug-fix spec; it's still a real concern on flaky network drives.

---

## 5. Memory

These are still applicable after the rework.

### M-1. Memoize `NormalizedPath.DisplayPath`

From archived `archive/SPEC_PERF_LOW_HANGING_FRUIT.md` P-1. `DisplayPath` is read on every `SourceCache.AddOrUpdate`, every Inspector field load, every log line. Today it allocates a substring per access. Memoize at construction.

**File.** `src/WinUiFileManager.Domain/ValueObjects/NormalizedPath.cs`. Compute `DisplayPath` in the constructor; ~15 LOC + 4 unit tests.

### M-2. Drop `NtfsFileId` from the enumeration-path entry model

`FileSystemEntryModel.NtfsFileId` is always `NtfsFileId.None` during enumeration; only the Inspector populates it on demand. Removing the field from the enumeration path saves a few bytes per entry × 100k items, and simplifies the record.

**Done when.** The model has no `NtfsFileId` field; the inspector still populates and renders it via its own deferred query.

### M-3. Intern file extensions in enumeration

Today 10k `.txt` files allocate 10k distinct extension strings during enumeration. A `Dictionary<string, string>` cache in `WindowsFileSystemService.BuildEntryModel` (or upstream in `FolderEnumerateService`) collapses them to one. Bounded growth: extensions per pane is small.

### M-4. `RetainVM = false`

Add `<RetainVMGarbageCollection>false</RetainVMGarbageCollection>` to `WinUiFileManager.App.csproj`. Tells the GC to decommit unused segments rather than retain them paged-out. Pairs with the post-navigation compacting Gen2 GC pattern in `MEMORY_OPTIMIZATION_RECOMMENDATIONS.md` §6.

**Verify.** After leaving a 100k folder for a small one, private bytes drop within a few seconds.

### M-5. Dispose-chain audit on shutdown

Today, `App.OnSuspending` does not exist; the app just closes. `MainShellView.OnUnloaded` disposes the data sources and unregisters from the messenger, but it relies on WinUI calling `Unloaded` reliably — true today, but worth a defensive teardown in `MainShellWindow` once persistence finishes.

---

## 6. Suggested order

The user's next-target work is Copy / Move / Delete with cancellation + rich diagnostics. The supporting prerequisites in this doc are I-1 (Coordinator), I-2 (FileOperationDialogService), D-1 (error model). Everything else is independently scheduleable and reversible.

Recommended sequence:

1. **I-1** Coordinator — unblocks everything else.
2. **I-2** FileOperationDialogService — ships the visible value (F5/F6/F7/F8 work).
3. **I-3** Drop hardcoded paths — small, removes a daily-papercut.
4. **I-4 / I-5** Refresh + Properties + Copy-path consumers — small, finish the keyboard surface.
5. **D-1** Error model — extends `OperationError` and lays the foundation for D-2..D-5.
6. **D-2 / D-3** Lock + disk-space diagnostics — highest-impact engineer-grade detail.
7. **D-6** Error display surface.
8. **S-1 / S-2** Refactor `MainShellView.xaml.cs` and `NtfsFileIdentityService` — the two most painful files. Do them after the diagnostics work so the new surface doesn't get split mid-flight.
9. **S-3 / S-4 / S-5 / S-6** — the inspector-VM, fileop-service, datasource, dialog refactors. Each is independent.
10. **St-1..St-5** stability passes — small, can interleave anywhere.
11. **M-1..M-5** memory passes — verify with profiling between steps; do not stack them blind.

Each item leaves the build green and main shippable.

---

## 7. Out of scope for this doc

- **Drag-and-drop**: permanent non-goal (see top).
- **Tabs, command palette, archive support, content preview, treemap**: still ideas in the archived `SPEC_FEATURE_LOW_HANGING_FRUIT.md`. Re-author against the new architecture if/when they come back into scope.
- **Long paths**: tracked in `SPEC_LONG_PATHS.md`. The path-class diagnostics in D-5 reuse types from that spec; the toolbar toggle and the broader UI gating remain in that spec.
- **Native modernization specifics**: the original spec lives at `archive/SPEC_NATIVE_MODERNIZATION.md` (archived because it doesn't reflect the current code). The cancellation-rethrow rule (St-4) is the only surviving piece that lands here; reach for the archive only if you need the historical scope.
- **Tooling**: covered in `SPEC_TOOLING_AND_ANALYZERS.md` (already shipped).
