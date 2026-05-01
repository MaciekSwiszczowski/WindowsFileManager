# Spec: Bug Fixes

Scope: correctness bugs identified during review. Each item has a cause, a fix, a file:line reference, and a verification step. Items are ordered by severity (crash/UX-breaking first).

Assumes `SPEC_TOOLING_AND_ANALYZERS.md` has landed; some fixes are easier once the analyzers flag related issues.

## B1. Idle CPU from periodic `Buffer` in the watcher pipeline

**Severity:** High. Fails the "0% CPU when inactive" requirement.

**File:** `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs:496-507`

**Cause.** `Observable.Buffer(TimeSpan, IScheduler)` is a periodic operator. It emits an empty `IList<T>` every `WatcherBufferWindow` (100 ms) regardless of source activity. Two active panes produce ~20 task-pool wake-ups per second at idle.

**Fix.** Replace the time-window buffer with an event-closing window that arms only after an event arrives:

```csharp
_directoryWatchSubscription = _directoryChangeStream
    .Watch(path)
    .Publish(source => source.Buffer(
        () => source.Throttle(WatcherBufferWindow, _schedulers.Background)))
    .Where(static batch => batch.Count > 0)
    .Select(batch => BuildWatcherBatch(watchedPath, batch))
    .ObserveOn(_schedulers.MainThread)
    .Subscribe(
        batch => ApplyWatcherBatch(watchedPath, batch),
        ex => _logger.LogError(ex, "Directory watcher pipeline failed for {Path}", watchedPath.DisplayPath));
```

**Verify.**
1. Launch, focus another window, wait 10 s. Task Manager shows 0.0-0.1% CPU.
2. Copy 5 000 files into the watched folder; batches still commit within 100-200 ms of quiescence.
3. Add a TUnit test using `Microsoft.Reactive.Testing.TestScheduler` that asserts zero emissions over 10 virtual seconds on a silent `IDirectoryChangeStream`.

## B2. ShellExecute receives the `\\?\`-prefixed path

**Severity:** High. Most file types fail to open with double-click.

**File:** `src/WinUiFileManager.Infrastructure/Services/WindowsShellService.cs:26`

**Cause.** `ProcessStartInfo.FileName = path.Value` passes the extended-length prefix. `ShellExecute` does not resolve file associations for `\\?\` paths and either fails silently or opens the wrong handler. `ShowPropertiesAsync` at line 45 already uses `DisplayPath`; the two paths are inconsistent.

**Fix.** Change to `FileName = path.DisplayPath`.

**Verify.** Double-click a `.txt`, `.md`, and `.png` in a pane; each opens in its registered handler.

## B3. Swallowed `OperationCanceledException` in `NtfsFileIdentityService`

> **Absorbed by `SPEC_NATIVE_MODERNIZATION.md` M-2.** Do not fix this ticket in a B-* batch — fix lands with the native-modernization cancellation-contract pass. If `SPEC_BUG_FIXES.md` B-1 lands before M-2, it skips B3; B-1 keeps only its B1 scope.

**Severity:** High. Inspector paints stale data for cancelled requests; violates the cancellation contract.

**File:** `src/WinUiFileManager.Infrastructure/FileSystem/NtfsFileIdentityService.cs`

Methods affected (each has a bare `catch` that swallows cancellation):
- `GetIdentityDetailsAsync` — line 75
- `GetNtfsMetadataDetailsAsync` — line 111
- `GetCloudDiagnosticsAsync` — line 179
- `GetLinkDiagnosticsAsync` — line 210
- `GetStreamDiagnosticsAsync` — line 249
- `GetSecurityDiagnosticsAsync` — line 287
- `GetThumbnailDiagnosticsAsync` — line 330

**Cause.** Inspector arms a 5 s timeout per batch (`FileInspectorViewModel.DeferredLoadTimeout`, line 21). When it fires, the `catch` block returns "fallback" data, which is then rendered as if authoritative.

**Fix.** In every affected method, re-throw cancellation before the generic catch:

```csharp
catch (OperationCanceledException)
{
    throw;
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "...");
    return fallback;
}
```

**Verify.** Add a TUnit test that passes a pre-cancelled `CancellationToken` to each method and asserts `OperationCanceledException` is raised.

## B4. Watcher restart storm on repeated error

**Severity:** Medium-High. A flapping network drive or AV filter can produce a sustained CPU/IO storm.

**File:** `src/WinUiFileManager.Infrastructure/FileSystem/WindowsDirectoryChangeStream.cs:149-172`

**Cause.** `OnError` disposes the watcher and calls `CreateAndStart` immediately. Each cycle emits `Invalidated`, which triggers a full pane rescan.

**Fix.** Add exponential back-off with a cap and a failure budget:

```csharp
private TimeSpan _currentBackoff = TimeSpan.FromMilliseconds(500);
private int _consecutiveFailures;
private const int MaxConsecutiveFailures = 10;
private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

private void OnError(object? sender, ErrorEventArgs e)
{
    FileSystemWatcher? toDispose;
    int failures;
    TimeSpan delay;

    lock (_gate)
    {
        if (_disposed) return;
        _logger.LogWarning(e.GetException(),
            "Directory watcher failed for {Path}. Emitting Invalidated and scheduling restart.", _path);
        toDispose = _watcher;
        _watcher = null;
        failures = ++_consecutiveFailures;
        delay = _currentBackoff;
        _currentBackoff = TimeSpan.FromTicks(Math.Min(_currentBackoff.Ticks * 2, MaxBackoff.Ticks));
    }

    toDispose?.Dispose();
    EmitInvalidated();

    if (failures > MaxConsecutiveFailures)
    {
        _logger.LogError("Giving up on watcher for {Path} after {Count} consecutive failures.",
            _path, failures);
        return;
    }

    _ = Task.Delay(delay).ContinueWith(_ =>
    {
        lock (_gate) { if (_disposed) return; }
        CreateAndStart();
    }, TaskScheduler.Default);
}
```

Reset `_consecutiveFailures = 0` and `_currentBackoff = TimeSpan.FromMilliseconds(500)` inside `OnCreated` / `OnChanged` / `OnRenamed` the first time they fire after a restart.

**Verify.** Integration test: watch a folder, simulate repeated errors via a test double, assert the restart rate is capped and gives up after 10 attempts.

## B5. Sync-over-async in `ResolveEntryViewModel`

**Severity:** Medium. Works today because the async method is synchronous under the hood; trap waiting to trigger.

**File:** `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs:621-624`

**Cause.** `.GetAwaiter().GetResult()` on `IFileSystemService.GetEntryAsync`. If the infrastructure ever returns a real async implementation, this stalls a TaskPool thread or deadlocks.

**Fix.** Add a synchronous sibling on the interface:

```csharp
// IFileSystemService.cs — add
FileSystemEntryModel? GetEntry(NormalizedPath path);
```

Implement in `WindowsFileSystemService`:

```csharp
public FileSystemEntryModel? GetEntry(NormalizedPath path)
{
    // existing body of GetEntryAsync, minus cancellation checks
}

public Task<FileSystemEntryModel?> GetEntryAsync(NormalizedPath path, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    return Task.FromResult(GetEntry(path));
}
```

Change `ResolveEntryViewModel` to call the sync method.

**Verify.** `grep "GetAwaiter().GetResult" src/` returns zero matches.

## B6. Volume probing blocks the UI thread

**Severity:** Medium. Navigation freezes on slow/offline network drives.

**Files:**
- `src/WinUiFileManager.Interop/Adapters/VolumeInterop.cs:11-37` (`GetVolumes`)
- `src/WinUiFileManager.Interop/Adapters/VolumeInterop.cs:42-77` (`GetVolumeForPath`)
- `src/WinUiFileManager.Infrastructure/Services/NtfsVolumePolicyService.cs:46-74` (`ValidateNtfsPath`)

**Cause.** `DriveInfo.VolumeLabel` and `DriveInfo.DriveFormat` each invoke `GetVolumeInformationW`, which blocks until the drive responds. `GetVolumes()` runs them serially for every drive during app startup (`MainShellViewModel.InitializeAsync:508`); `ValidateNtfsPath` runs them on every navigation.

**Fix.**
1. Cache the file-system format per drive letter in `NtfsVolumePolicyService` (dictionary keyed on uppercase letter). Invalidate on `WM_DEVICECHANGE` or on explicit refresh. NTFS does not change at runtime, so cache is always valid for the lifetime of a mount.
2. Wrap the initial `GetVolumes()` in `Task.Run` in the repository layer. Probe each drive with a 500 ms timeout using `Task.WhenAny(probe, Task.Delay(500))`; if the probe loses, mark the drive as "Unknown" and exclude it from the NTFS filter.
3. `ValidateNtfsPath` becomes a dictionary lookup (O(1), no syscall) after the first hit.

**Verify.** Disconnect a mapped network drive; start the app; confirm startup completes within 2 s and the other drives remain usable. Navigate the remaining pane; no stall.

## B7. `async void` property setter

**Severity:** Low-Medium. Latent crash vector if body is extended.

**File:** `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:83-95`

**Cause.** `OnParallelExecutionEnabledChanged` is `async void`. Body currently has a try/catch, but any future extension can leak an unhandled exception and terminate the process.

**Fix.** Expose the toggle as an `IAsyncRelayCommand`:

```csharp
[RelayCommand]
private async Task SetParallelExecutionAsync(bool enabled)
{
    try
    {
        await _setParallelExecutionHandler.ExecuteAsync(enabled, 4, CancellationToken.None);
        _currentSettings = await _settingsRepository.LoadAsync(CancellationToken.None);
        OnPropertyChanged(nameof(ParallelExecutionEnabled));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to update parallel execution setting");
    }
}
```

Bind the toolbar `AppBarToggleButton` to `SetParallelExecutionCommand` via `CommandParameter="{x:Bind IsChecked}"` and drop the setter from `ParallelExecutionEnabled` (make it read-only).

**Verify.** Analyzer `VSTHRD100` / `MA0045` does not flag the file after the change.

## B8. `JsonFavouritesRepository.GetAllAsync` wipes list on one bad row

**Severity:** Medium. Unexpected data loss.

**File:** `src/WinUiFileManager.Infrastructure/Persistence/JsonFavouritesRepository.cs:34-46`

**Cause.** `ToDomain(dto)` calls `NormalizedPath.FromUserInput(dto.Path)` which throws `ArgumentException` on empty input. The exception propagates out of `Select` → `ToList()`, killing the whole read.

**Fix.** Convert per-item with a try/catch that logs and skips invalid rows:

```csharp
private IReadOnlyList<FavouriteFolder> ConvertDtos(IReadOnlyList<FavouriteDto> dtos)
{
    var folders = new List<FavouriteFolder>(dtos.Count);
    foreach (var dto in dtos)
    {
        try
        {
            folders.Add(ToDomain(dto));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping malformed favourite {Id} with path {Path}", dto.Id, dto.Path);
        }
    }
    return folders;
}
```

Apply the same pattern to `JsonSettingsRepository` (a bad persisted path on any of the two pane paths must not wipe the whole settings object; reset just that field).

**Verify.** Hand-edit `%LocalAppData%\WinUiFileManager\favourites.json` to add a favourite with `"path": ""`; restart; other favourites still load; warning in the log.

## B9. Shell COM file-lock probe requires STA access

> **Absorbed by `SPEC_NATIVE_MODERNIZATION.md` M-2.** Do not fix this ticket in a B-* batch — the native-modernization pass removes the shell `IFileIsInUse` lock probe instead of trying to harden the RCW lifetime. If `SPEC_BUG_FIXES.md` B-4 lands before M-2, it skips B9.

**Severity:** Low. Optional file-lock metadata came from shell COM, which requires STA access and is not supported consistently by applications.

**File:** removed file-identity interop wrapper / shell `IFileIsInUse` probe.

**Cause.** `IFileIsInUse` is a shell COM probe. It can add STA constraints to inspector diagnostics for information that is only advisory.

**Fix.** Remove the file-identity interop wrapper and shell `IFileIsInUse` probe. Keep lock detection to Restart Manager data only.

**Verify.** Search `src/` for `IFileIsInUse`, `SHCreateItemFromParsingName`, and the file-identity interop wrapper; no source matches remain.

## B10. `NavigateUpAsync` `GetParentPath` helper is fragile on `\\?\` paths

**Severity:** Low. Edge case in navigation when the drive itself disappears.

**File:** `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs:712-725`

**Cause.** The short-circuit pattern `trimmedPath is [_, ':']` only matches `"C:"`. On an extended-length input `"\\?\C:"` it falls through, and `Path.GetDirectoryName("\\?\C:")` returns the invalid root `"\\?\"`.

**Fix.** Normalize the parent path through `NormalizedPath` first. Short-circuit when the normalized value is a drive root:

```csharp
private static string? GetParentPath(NormalizedPath path)
{
    var display = path.DisplayPath;
    if (display.Length <= 3 && display.EndsWith(":\\", StringComparison.Ordinal))
    {
        return null;
    }
    var parent = Path.GetDirectoryName(display.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    return string.IsNullOrEmpty(parent) ? null : parent;
}
```

Threads through `ResolveExistingDirectoryOrAncestorAsync` so the ancestor walk always operates on display paths.

**Verify.** Unit test: seed a pane at `"C:\\"`; call `NavigateUpCommand.Execute`; confirm no error and no navigation occurs.

## B11. `WinUiClipboardService.SetTextAsync` blocks the UI thread

**Severity:** Low. 50-200 ms UI stall when clipboard listeners are slow.

**File:** `src/WinUiFileManager.Presentation/Services/WinUiClipboardService.cs:8-18`

**Cause.** `Clipboard.Flush()` is synchronous on whatever thread calls it.

**Fix.** Move to a background task:

```csharp
public Task SetTextAsync(string text, CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    return Task.Run(() =>
    {
        var pkg = new DataPackage();
        pkg.SetText(text);
        Clipboard.SetContent(pkg);
        Clipboard.Flush();
    }, ct);
}
```

Note: `Clipboard.SetContent` and `Clipboard.Flush` require the WinRT STA; they may throw `COMException` off the UI thread on some Windows builds. If that materializes, fall back to `DispatcherQueue.TryEnqueue` and await through a `TaskCompletionSource`.

**Verify.** Install a slow clipboard listener (or write one); confirm no UI stall on Ctrl+Shift+C.

## B12. `FilePaneViewModel.ResolveDirectoryPathAsync` runs validators on UI thread

**Severity:** Medium. Related to B6 but specifically about path validation.

**File:** `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs:377-404`

**Cause.** `_volumePolicyService.ValidateNtfsPath(...)` (see B6) and `_fileSystemService.DirectoryExistsAsync(...)` are invoked on whatever thread called `NavigateToCommand`. That's the UI thread for user input.

**Fix.** After B6 ships, `ValidateNtfsPath` is a dict lookup. `DirectoryExistsAsync` is still a potential blocker on a slow path; marshal through `Task.Run`:

```csharp
var exists = await Task.Run(() => _fileSystemService.DirectoryExistsAsync(normalizedPath, cancellationToken),
                             cancellationToken);
```

Simpler: change `WindowsFileSystemService.DirectoryExistsAsync` to `return Task.Run(() => Directory.Exists(path.DisplayPath), ct)` so every caller is safe.

**Verify.** Attempt to navigate to a slow network path; the path textbox and toolbar remain responsive during the probe.

## B13. `MainShellWindow.OnAppWindowClosing` double-close race

**Severity:** Low. Benign, but worth tightening.

**File:** `src/WinUiFileManager.App/Windows/MainShellWindow.xaml.cs:67-85`

**Cause.** Sets `_statePersisted = true` before awaiting `PersistStateAsync`. If another close signal arrives during the await, the guard returns and the window closes without completing persistence. The `try/finally` compensates, but the flag semantics are confused.

**Fix.** Gate with a `TaskCompletionSource`:

```csharp
private readonly TaskCompletionSource _closeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
private int _closing;

private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
{
    if (Interlocked.Exchange(ref _closing, 1) == 1)
    {
        args.Cancel = true;
        await _closeGate.Task;
        this.Close();
        return;
    }

    args.Cancel = true;
    try
    {
        if (_viewModel is not null) await _viewModel.PersistStateAsync();
    }
    catch (Exception ex)
    {
        // logger available via App.Services if desired
    }
    finally
    {
        _closeGate.TrySetResult();
        this.Close();
    }
}
```

**Verify.** Rapidly press Alt+F4 twice; no crash, state is persisted exactly once.

## B14. Inspector `CopyAllAsync` LINQ chain on every click

**Severity:** Low. Tiny GC hit; included for completeness because analyzer flags it.

**File:** `src/WinUiFileManager.Presentation/ViewModels/FileInspectorViewModel.cs:165-181`

**Cause.** `Categories.Where(...).OrderBy(...)` + inner `Fields.Where(...).OrderBy(...)` allocates several enumerator pipelines per invocation.

**Fix.** Pre-sort `Categories` at insert time (already partially done in `GetOrCreateCategory`). Replace LINQ with a pre-sorted `_sortedCategoriesByOrder` list:

```csharp
private readonly List<FileInspectorCategoryViewModel> _sortedCategories = new();
```

Populate in sorted order inside `GetOrCreateCategory`. In `CopyAllAsync`, iterate `_sortedCategories` directly.

**Verify.** `Copy All` output unchanged. Analyzer `MA0020` (avoid LINQ Where+OrderBy) no longer fires.

---

## Acceptance

- Every item above has a merged PR with the fix and, where called for, a regression test.
- `dotnet-counters monitor -n WinUiFileManager.App System.Runtime` at idle shows `cpu-usage` ~ 0.0 for 60 s.
- `dotnet test` passes.
- Manual smoke on a machine with a disconnected network drive: app starts in < 2 s and navigation on other drives is responsive.
- Manual smoke on a folder with 100 000 files: no new regressions introduced (perf work covered in a separate spec).
