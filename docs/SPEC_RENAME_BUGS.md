# Spec: Rename Bug Fixes

Scope: three observed defects in the in-cell rename feature shipped in batch U-4 (`SPEC_UI_LAYOUT_AND_RESIZING.md` §6), plus the hardening needed so commits survive concurrent filesystem activity. The detailed `FileEntryTableView` contract now lives in `SPEC_FILE_ENTRY_TABLE_VIEW.md`; this spec only defines the rename-hardening work layered on top of that control contract. The spec is prescriptive — properties, method signatures, and test scenarios are named exactly. Agents do not invent alternatives.

Landing order: **right after `SPEC_UI_LAYOUT_AND_RESIZING.md` closes**, before any keyboard-shortcut or native-modernization work.

## 1. Goals

1. **R1 — selection stays on the renamed row.** Pressing Enter to commit must leave the renamed item as the sole selected row and the pane's `CurrentItem`; arrow keys must resume from there.
2. **R2 — collisions and handler failures are visible and dismissible.** When a rename returns an error (collision, access denied, invalid name, etc.), the user sees a short message in an inline `InfoBar`, keeps the editor open with the typed name intact, and can dismiss the banner to continue editing.
3. **R3 — concurrent filesystem activity cannot crash or silently mis-edit the UI.** External renames, deletes, or writes happening while the user edits a name are surfaced safely. No `FileNotFoundException` escapes the handler into a silent no-op; no ghost row steals focus; no `File.Move` is issued against a file that no longer exists at the captured path.

## 2. Root-cause map

The three bugs share a common seam: the rename flow captures `entry.Model.FullPath` at `BeginRenameCurrent` time and never re-anchors after the handler returns. Specific culprits:

### 2.1. R1 — row-selection flip after Enter

- [FilePaneViewModel.CommitRenameAsync](../src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs) (≈ line 415) does not capture the expected destination path. On success it clears `IsEditing`, sets `_activeEditingEntry = null`, and returns `true`.
- The actual filesystem rename triggers a watcher `Renamed` event. `ApplyWatcherBatch` (≈ line 715) has a `RenamedPaths` remap for the 100 ms buffer window; if the event lands outside that window (common in practice), the remap never matches.
- In the meantime, `_sourceCache.Remove(oldPath) + AddOrUpdate(newEntry)` drives a DynamicData change set. The TableView treats these as "unrelated remove + add"; its `SelectedItem` snaps onto the neighboring row.
- [FileEntryTableView.FileTable_SelectionChanged](../src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml.cs) (≈ line 337) echoes the TableView's new `SelectedItem` into `host.CurrentItem`, so the VM's `CurrentItem` also moves to the neighbor.
- `UniqueKey = Model.FullPath.DisplayPath` (FileEntryViewModel.cs ≈ line 61) mutates on rename, so re-selection by existing key is impossible without an explicit pre-commit capture.

### 2.2. R2 — silent collision

- `RenameEntryCommandHandler.ExecuteAsync` maps `File.Move(src, dst, overwrite: false)` throwing `IOException` HResult `0x80070050` (ERROR_FILE_EXISTS) through the interop layer (`FileOperationInterop.cs` ≈ line 30) to `FileOperationErrorCode.DestinationExists` and returns an `OperationSummary` with `Status = Failed`.
- `CommitRenameAsync` inspects neither the `Status` nor the per-item `Error.Code`; it logs at `Debug` level and returns `false`.
- `FileEntryTableView.FileTable_CellEditEnding` (≈ line 496) reacts to the `false` return by cancelling the TableView commit (`e.Cancel = true`) and re-focusing the editor. No banner, no toast, no status text — the user sees nothing.
- `FilePaneViewModel.ErrorMessage` is bound to the pane's loading overlay (FilePaneView.xaml.cs ≈ line 273) and is the wrong channel: surfacing the error there covers the grid and defeats "keep editing". The app has no `InfoBar` / `TeachingTip` elsewhere.

### 2.3. R3 — races with external writers

- The captured `entry.Model.FullPath` is a value-type snapshot. An external rename of the same file between `BeginRenameCurrent` and `CommitRenameAsync` leaves the captured path stale; the subsequent `File.Move(oldCaptured, newDest)` throws `FileNotFoundException`.
- `FileNotFoundException` and `DirectoryNotFoundException` both inherit from `IOException` and are caught at `FileOperationInterop.cs` (≈ line 33), but the HResult mapping in `WindowsFileOperationService.cs` (≈ line 389) routes HResult 2 / 3 through the same "unknown failure" bucket that emits no distinct error code. The summary returns `Failed` with a generic message and the VM swallows it (see R2).
- The watcher's `Deleted` event also races with the commit. `ApplyWatcherBatch` does `_sourceCache.Remove(oldPath)` → DynamicData propagates → `FileTable_SelectionChanged` moves `CurrentItem` to a neighbor → `OnCurrentItemChanged` (≈ line 477) detects `_activeEditingEntry != value` and calls `CancelRename`, closing the editor mid-typing without feedback.
- Two concurrent commits on the same entry are short-circuited by `ReferenceEquals(_activeEditingEntry, entry)` at `CommitRenameAsync` (≈ line 420) — not a live bug today but worth documenting as an invariant.

## 3. Target behavior

### 3.1. R1 — selection & focus restoration

Add a private field to `FilePaneViewModel`:

```csharp
private NormalizedPath? _expectedRenameTarget;
```

Set it in `CommitRenameAsync` *before* awaiting the handler:

```csharp
var newFullPath = entry.Model.FullPath.Parent.Append(newName); // or equivalent
_expectedRenameTarget = newFullPath;
try
{
    var summary = await _renameHandler.ExecuteAsync(entry.Model, newName, ct);
    if (summary.FailedCount == 0 && summary.Status == OperationStatus.Succeeded)
    {
        ApplyInFlightRename(entry, newFullPath);
        entry.IsEditing = false;
        entry.EditBuffer = string.Empty;
        _activeEditingEntry = null;
        return true;
    }
    /* … failure path, see §3.2 … */
}
finally
{
    // Keep _expectedRenameTarget set so ApplyWatcherBatch can suppress echo;
    // clear it after a debounce window (see below) or on next Begin/Cancel.
}
```

`ApplyInFlightRename(entry, newFullPath)` is a new helper that:

1. Builds a new `FileEntryViewModel` with a model whose `FullPath` is `newFullPath` (keep every other field of the `FileSystemEntryModel` — Size, LastWriteTime, Attributes, etc. — copied from the old one).
2. Inside a `_sourceCache.Edit(updater => { updater.RemoveKey(oldUniqueKey); updater.AddOrUpdate(newViewModel); })` block so DynamicData emits a single change set.
3. Sets `CurrentItem = newViewModel` under `_syncingSelection = true` semantics.
4. Emits a new event `RenameCommitted(entry, newViewModel)` so the view can re-focus the row (see §3.4).

`ApplyWatcherBatch` (`FilePaneViewModel.cs`) consults `_expectedRenameTarget` at the top of its batch loop: if the batch's `RenamedPaths` or its `(AddedPaths, RemovedPaths)` pair covers the expected `(oldPath, newPath)` transition, the VM treats those items as already-applied and drops them from the batch. The field is cleared after consumption or after a 500 ms timeout (reuse the existing `WatcherBufferWindow` constant).

Tests (`tests/WinUiFileManager.Application.Tests/Scenarios/ViewModelRenameCommandTests.cs`):

- `Test_CommitRenameAsync_PreservesCurrentItemOnSuccess` — rename `old.txt → new.txt` in a pane of 3 files; assert `vm.LeftPane.CurrentItem.Name == "new.txt"`, `vm.LeftPane.SelectedCount == 1`, and the only selected entry matches `CurrentItem`.
- `Test_CommitRenameAsync_SuppressesWatcherEchoForOwnRename` — after success, fire a synthetic watcher batch whose `RenamedPaths` contains the same `(old, new)` pair; assert no `PropertyChanged` for `CurrentItem` fires and `ActiveEditingEntry` stays null.

### 3.2. R2 — collision UX

Add to `FilePaneViewModel`:

```csharp
public readonly record struct RenameErrorInfo(RenameErrorCode Code, string Message);

public enum RenameErrorCode
{
    DestinationExists,
    AccessDenied,
    SourceGone,
    PathTooLong,
    InvalidCharacters,
    Unknown,
}

[ObservableProperty]
public partial RenameErrorInfo? RenameError { get; set; }
```

Mapping in `CommitRenameAsync` on the failure path:

```csharp
var firstError = summary.ItemResults.FirstOrDefault(r => !r.Succeeded)?.Error;
RenameError = firstError?.Code switch
{
    FileOperationErrorCode.DestinationExists => new(
        RenameErrorCode.DestinationExists,
        $"A file or folder named \"{newName}\" already exists in this folder."),
    FileOperationErrorCode.AccessDenied => new(
        RenameErrorCode.AccessDenied,
        "You don't have permission to rename this file."),
    FileOperationErrorCode.SourceGone => new( // see §3.3 for the new code
        RenameErrorCode.SourceGone,
        "This file was deleted or moved by another process."),
    FileOperationErrorCode.PathTooLong => new(
        RenameErrorCode.PathTooLong,
        "The new name makes the path too long."),
    _ => new(
        RenameErrorCode.Unknown,
        firstError?.Message ?? summary.Message ?? "Rename failed."),
};
```

`BeginRenameCurrent` and `CancelRename` clear `RenameError` to `null` so a stale banner never persists into a new edit. `CommitRenameAsync`'s success path also clears it.

For invalid-character input, the existing early-return branch (`newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0`) now sets:

```csharp
RenameError = new(
    RenameErrorCode.InvalidCharacters,
    "Names cannot contain \\ / : * ? \" < > |.");
return false;
```

`FilePaneViewModel.ErrorMessage` is untouched — it remains the loading-overlay channel. Rename errors do not cover the grid.

UI binding (`src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml`):

- Above the `TableView` (or below the path box, inside the same `Grid.Row`), add an `InfoBar` with `Severity="Error"` (`Warning` for `InvalidCharacters`), `IsClosable="True"`, bound one-way to `RenameError`.
- Binding uses a `NullToVisibilityConverter` (or `x:Bind` with a null-check function) so the `InfoBar` stays collapsed when `RenameError == null`.
- `IsOpen` is two-way: clicking the `X` flips it to `false`, which in the code-behind calls `ViewModel.ClearRenameError()` (a new public method that sets `RenameError = null`). Focus returns to the editor; `EditBuffer` is preserved so the user can fix and retry.

Keyboard:

- Escape on the editor still cancels the edit (existing behavior via the TableView's `CellEditEnding` with `TableViewEditAction.Cancel`).
- Escape while the `InfoBar` has focus dismisses the banner only (default `InfoBar` behavior — do not re-implement).

Tests:

- `Test_CommitRenameAsync_SetsRenameErrorOnCollision` — existing `Test_CommitRenameAsync_CollisionKeepsEditOpen` is updated to additionally assert `RenameError.Value.Code == RenameErrorCode.DestinationExists` and that the message mentions the chosen name.
- `Test_BeginRenameCurrent_ClearsStaleRenameError` — set `RenameError` manually, call `BeginRenameCurrent`; assert `RenameError == null`.
- `Test_InvalidCharacters_SetsRenameErrorAndKeepsEditing` — commit with `newName = "bad/name.txt"`; assert `RenameError.Code == InvalidCharacters` and `IsEditing == true`.

### 3.3. R3 — race hardening

Three concrete changes:

1. **Pre-commit existence re-check.** `CommitRenameAsync`, right before setting `_expectedRenameTarget`, looks up the entry by its *current* unique key in `_sourceCache`:

   ```csharp
   var lookup = _sourceCache.Lookup(entry.UniqueKey);
   if (!lookup.HasValue)
   {
       RenameError = new(
           RenameErrorCode.SourceGone,
           "This file was deleted or moved by another process.");
       CancelRename(entry);
       return false;
   }
   ```

   The editor closes (the entry is gone — re-editing is pointless), the banner reports the cause, and no `File.Move` is issued.

2. **Interop catch tightening.** Add a new enum value `FileOperationErrorCode.SourceGone`. In [FileOperationInterop.cs](../src/WinUiFileManager.Interop/Adapters/FileOperationInterop.cs) (≈ lines 26-41), add explicit catches ahead of the generic `IOException`:

   ```csharp
   catch (FileNotFoundException ex)
   {
       return InteropResult.Fail(FileOperationErrorCode.SourceGone, ex.Message);
   }
   catch (DirectoryNotFoundException ex)
   {
       return InteropResult.Fail(FileOperationErrorCode.SourceGone, ex.Message);
   }
   ```

   The HResult mapping in [WindowsFileOperationService.cs](../src/WinUiFileManager.Infrastructure/Services/WindowsFileOperationService.cs) (≈ line 380-392) also maps HResult 2 / 3 to `SourceGone` so direct Win32 callers land on the same code.

3. **Watcher suppression while editing.** In `FilePaneViewModel.ApplyWatcherBatch`, when a batch removes or renames an entry that equals `_activeEditingEntry` *and* `_expectedRenameTarget` is null (so this is not our own rename echoing back):
   - Do **not** run the `_sourceCache.Remove` for that key yet.
   - Set `RenameError = new(RenameErrorCode.SourceGone, …)`.
   - Leave `IsEditing = true` so the user sees the banner over the still-visible editor.
   - When the user clicks the `InfoBar` close (or presses Escape in the editor), `ClearRenameError` triggers a `DeferredWatcherApply` that re-processes the buffered change: `_sourceCache.RemoveKey(entry.UniqueKey)`; the editor auto-closes via `OnCurrentItemChanged`.

   Keep the suppression window bounded: if `RenameError` stays set for more than 30 s, apply the pending removal anyway (avoid an editor that hangs the pane indefinitely).

4. **Two-concurrent-commits invariant.** No code change; add a code comment on `_activeEditingEntry` (≈ line 45) noting the re-entrancy contract: `BeginRenameCurrent` always resets the sentinel; `CommitRenameAsync` short-circuits via `ReferenceEquals`; callers must not invoke `CommitRenameAsync` from a background thread. Unit test this invariant indirectly via `Test_DoubleCommit_OnlyFirstRuns` (start a commit, immediately request a second before the first returns; assert the second returns `false` without calling the handler).

Tests:

- `Test_CommitRenameAsync_SourceDeletedBeforeCommit_SurfacesSourceGone` — precondition: `File.Delete(sourcePath)` between `BeginRenameCurrent` and `CommitRenameAsync`; assert `RenameError.Code == SourceGone`, `IsEditing == false`, and the handler was not called.
- `Test_ExternalDeleteDuringEdit_KeepsEditorOpenWithBanner` — simulate a watcher `Removed` event while `_activeEditingEntry` is set; assert `IsEditing` stays true until `ClearRenameError()` is called.
- `Test_ExternalRenameDuringEdit_SurfacesBannerWithCorrectMessage` — simulate watcher `Renamed` event on the editing entry; assert banner says "deleted or moved" and original editor stays up.
- `Test_InteropTranslatesFileNotFoundToSourceGone` — unit test `FileOperationInterop.Rename` with a path that was deleted right before the call; assert `ErrorCode == SourceGone`.

### 3.4. View wiring

`FileEntryTableView.xaml.cs` (`src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml.cs`):

- Subscribe to `FilePaneViewModel.RenameCommitted` (new event from §3.1); the handler re-selects the renamed **body-table** row and scrolls it into view using the existing rename-focus path (minus the edit). This closes the keyboard-anchor gap described in R1.
- On `RenameError` transitioning to non-null, keep focus on the editor's `TextBox` (do not re-focus the TableView row).

`FilePaneView.xaml.cs`:

- Add a private `ClearRenameErrorClick` handler wired to the `InfoBar.CloseButtonClick` event; it calls `ViewModel.ClearRenameError()` and sets focus to the active editor via the existing `TryFocusNameEditor` path.

## 4. Out of scope for this spec

- Red `VisualState` flash on the TextBox — previously discussed during UI-layout batching, but superseded by the `InfoBar` direction in this spec.
- Toasts / notifications for successful renames. Silent success is the right UX.
- Batch / multi-select rename. Non-goal per `SPEC_UI_LAYOUT_AND_RESIZING.md` §10.
- General `FilePaneViewModel` concurrency audit. Only the rename path is hardened here.
- Undo/redo. Non-goal.

## 5. Ancillary items (not batched, just flagged)

- `ResolveEntryViewModel` (`FilePaneViewModel.cs` ≈ line 779) uses `GetAwaiter().GetResult()` off the UI thread. Not a rename bug; track under `SPEC_BUG_FIXES.md` B-5.
- `FileTable_CellEditEnding` is `async void` (required by WinUI event contract); do not attempt to "fix" it.
- Logging: the current `LogDebug("Inline rename rejected for {Path}: {Message}", …)` line stays; add a matching `LogWarning` in the `Unknown` failure path so unknown handler errors are captured in production logs.

## 6. Interactions with other specs

- `SPEC_UI_LAYOUT_AND_RESIZING.md` §6.7 (validation semantics) is amended: "edit-stays-open" is preserved, but an `InfoBar` banner now surfaces the reason. See this spec's §3.2.
- `SPEC_BUG_FIXES.md` — the rename-path race work here is distinct from the general bug clusters; B1–B14 stay as-is. (B2 path-too-long logic is still deferred to `SPEC_LONG_PATHS.md`.)
- `SPEC_LONG_PATHS.md` §6 capability gating for `RenameEntry` is unaffected — rename still works on long paths and the `SourceGone` / `PathTooLong` messages apply equally.
- `SPEC_NATIVE_MODERNIZATION.md` M-2 and M-3 modernize the native boundary the rename flow depends on, but do not change its behavior.

## 7. Acceptance

This spec is complete when:

- All three bugs have a regression test that fails before the batch lands and passes after.
- `FilePaneViewModel` exposes `RenameError` and `ClearRenameError()`; `BeginRenameCurrent` / `CancelRename` / success-path all clear it; `CommitRenameAsync` sets it on every non-success code path.
- `FileOperationErrorCode.SourceGone` exists and is mapped from `FileNotFoundException` / `DirectoryNotFoundException`.
- `FilePaneView.xaml` renders an `InfoBar` bound to `RenameError`; dismissing it returns focus to the editor with `EditBuffer` preserved.
- `FilePaneViewModel.ApplyWatcherBatch` consults `_expectedRenameTarget` to suppress self-rename echoes and treats external delete/rename of `_activeEditingEntry` as banner-only until user dismissal.
- `dotnet build -warnaserror` and `dotnet test` are green on Release|x64.
- Manual checklist below passes on a Windows 11 workstation (100% + 150% DPI).

### 7.1. Manual checklist

- [ ] Rename a file in a 50-item folder. After Enter, the renamed item is the only selected row and remains current; arrow keys move from it to the next item by sort order.
- [ ] Rename to a name that already exists. `InfoBar` appears with the collision message; editor stays open with the typed name; `X` dismisses the banner and returns focus to the editor; retype a unique name and press Enter — rename succeeds.
- [ ] Rename to a name containing `\`. `InfoBar` shows the invalid-character message; editor stays open; fix the name and commit — success.
- [ ] Begin rename, switch to a terminal, `del` the source file, return to the app, press Enter. `InfoBar` shows the "deleted or moved" message and the editor closes. No crash.
- [ ] Begin rename, in a terminal `ren` the source file to something else, return to the app. `InfoBar` appears over the still-open editor; pressing `X` dismisses it and the entry disappears from the pane.
- [ ] Begin rename on a 400-char long path; commit; success. Long-path gating does not regress.
