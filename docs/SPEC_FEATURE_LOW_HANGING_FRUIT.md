# Spec: Low-Hanging-Fruit Features

Scope: small, self-contained feature additions that deliver disproportionate value for a dev-facing, keyboard-first, dual-pane file manager. Every feature respects the contract in `AGENT_BRIEF.md` (keyboard-first, NTFS-only, dual pane, no admin required unless noted).

Each feature lists:
- **What** — the user-visible behavior.
- **Why** — the developer value.
- **Where** — files touched and new files.
- **How** — enough implementation detail to hand to an agent.
- **Done when** — an acceptance checklist.

Features are independent; pick any order. Suggested sequencing at the bottom.

---

## F1. Quick-filter box (`Ctrl+F`)

**What.** A text filter above the file list in the active pane. As the user types, the file list filters to entries whose name contains the substring (case-insensitive). `Esc` clears. `Enter` moves focus back to the list.

**Why.** The existing incremental search (prefix-match on key-press) is transient and first-char only. A persistent filter is the single biggest usability win on 100K folders.

**Where.**
- `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs` — add `FilterText` observable property and filter predicate wiring.
- `src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml` — add a `TextBox` row above the table (visible only when non-empty or when Ctrl+F triggered).
- `src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml.cs` — wire keyboard shortcut.

**How.** The pane already uses DynamicData. Add a filter step:

```csharp
private readonly BehaviorSubject<Func<FileEntryViewModel, bool>> _filterPredicate = new(static _ => true);

// in ctor, extend the subscription:
_subscription = _sourceCache.Connect()
    .Filter(_filterPredicate)
    .ObserveOn(_schedulers.MainThread)
    .SortAndBind(out _sortedItems, _sortComparer.AsObservable())
    .Subscribe();

partial void OnFilterTextChanged(string? value)
{
    var term = value?.Trim();
    _filterPredicate.OnNext(string.IsNullOrEmpty(term)
        ? static _ => true
        : e => e.IsParentEntry || e.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
}
```

Bind the XAML `TextBox.Text` two-way to `FilterText`. In `MainShellView.OnPreviewKeyDown`, handle `VirtualKey.F when ctrl` to set focus to the active pane's `FilterBox` and select all.

**Done when.**
- Ctrl+F focuses the active pane's filter box.
- Typing filters in real time.
- Esc clears and restores full list.
- Sort, selection, and watcher updates still work while filter is active.
- Status bar shows "`N` items shown (of `M` total)" when filter is non-empty.

---

## F2. Directory tabs per pane

**What.** Each pane has a row of tab chips. `Ctrl+T` duplicates the current folder into a new tab; `Ctrl+W` closes the current tab; `Ctrl+Tab` cycles.

**Why.** Developers frequently flip between 3-5 folders per pane. Tabs remove the navigate-back-and-forth friction.

**Where.**
- New file: `src/WinUiFileManager.Presentation/ViewModels/FilePaneHostViewModel.cs` — owns a `List<FilePaneViewModel>` and an active index.
- `src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml` — add a `TabView` (from Microsoft.UI.Xaml.Controls) above the path row.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — swap `LeftPane`/`RightPane` for `LeftPaneHost`/`RightPaneHost` + helper properties `ActivePane` that dereferences the active tab.
- `src/WinUiFileManager.Application/Settings/AppSettings.cs` — persist per-pane tab paths + active tab index.

**How.**
1. Extract the existing `FilePaneView` content into a `FolderTabView` control; host it inside a `TabView`.
2. `FilePaneHostViewModel.AddTabCommand(NormalizedPath path)` creates a new `FilePaneViewModel` (through the DI container's `IServiceProvider.GetRequiredService<FilePaneViewModel>()`) and appends it.
3. Dispose old `FilePaneViewModel` on tab close (already `IDisposable`).
4. Persist on shutdown; rehydrate on startup.

**Done when.**
- Ctrl+T adds a new tab at the current folder.
- Ctrl+W closes (never the last tab — that clears the folder instead).
- Ctrl+Tab / Ctrl+Shift+Tab cycles.
- Tabs survive app restart.
- Middle-click on a tab closes it.

---

## F3. Command palette (`Ctrl+Shift+P`)

**What.** A search-as-you-type overlay listing every command in the app (toolbar, shortcuts, favourites). Executes the matching command.

**Why.** Discoverability. Users learn shortcuts organically. Also a fast path for rarely-used commands.

**Where.**
- New file: `src/WinUiFileManager.Presentation/Views/CommandPaletteDialog.xaml(.cs)`.
- New file: `src/WinUiFileManager.Presentation/ViewModels/CommandPaletteViewModel.cs`.
- Trigger: `MainShellView.OnPreviewKeyDown` on `Ctrl+Shift+P`.

**How.**
1. Define a static `CommandDescriptor[]` built once: `(string Title, string? Shortcut, Action Invoke)`. Populate from a reflection pass over `MainShellViewModel`'s `[RelayCommand]`-generated `*Command` properties plus favourites.
2. The palette is a `ContentDialog` with a `TextBox` + filtered `ListView`. Fuzzy match on title (simple substring is fine for v1).
3. Up/Down navigates; Enter invokes; Esc closes.

**Done when.**
- All toolbar commands appear in the palette.
- Favourites appear in the palette with "Open favourite:" prefix.
- Typing filters in < 10 ms.
- Invocation closes the palette and runs the command.

---

## F4. "Reveal in Explorer" and "Open Terminal Here"

**What.**
- `Ctrl+Shift+E` — open Windows Explorer with the active pane's current item selected.
- `Ctrl+Shift+T` — open Windows Terminal (or fall back to `cmd.exe`) in the active pane's current folder.

**Why.** Developers cross-tool constantly. Two new command handlers, near-zero maintenance.

**Where.**
- `src/WinUiFileManager.Application/Abstractions/IShellService.cs` — extend.
- `src/WinUiFileManager.Infrastructure/Services/WindowsShellService.cs` — implement.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — wire commands.
- `src/WinUiFileManager.Presentation/Views/MainShellView.xaml(.cs)` — bind keys.

**How.**
1. `Task RevealInExplorerAsync(NormalizedPath path, CancellationToken ct)` — `Process.Start("explorer.exe", $"/select,\"{path.DisplayPath}\"")` via `ProcessStartInfo`. No `UseShellExecute`.
2. `Task OpenTerminalAsync(NormalizedPath folder, CancellationToken ct)` — try `wt.exe -d "{folder}"` first; fall back to `cmd.exe /K cd /D "{folder}"` if `wt.exe` resolution fails. Never elevate.

**Done when.**
- Shortcut works with no selection (falls back to the pane's current folder for Reveal; uses it for Terminal).
- Works on paths containing spaces and parentheses.

---

## F5. Breadcrumb path header

**What.** Replace the `TextBox` path field with a clickable breadcrumb (`C: > Users > me > Projects > foo`). Clicking a segment navigates the pane. Ctrl+L still focuses the edit mode (textbox overlay).

**Why.** Explorer-equivalent feature, expected by users.

**Where.**
- `src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml(.cs)`.

**How.** Use a `ItemsRepeater` bound to a `IReadOnlyList<BreadcrumbSegment>` derived from `CurrentNormalizedPath`. Ctrl+L flips a `IsEditing` flag that swaps the visible element to the existing `PathBox`.

**Done when.**
- Breadcrumb updates as the pane navigates.
- Clicking any segment navigates.
- Ctrl+L still focuses the path textbox for free-form input.

---

## F6. Recursive folder size (`Alt+Enter` on a directory)

**What.** Shift the Properties action to also compute and display size-on-disk recursively when the selection is a directory. Show progress and allow cancel.

**Why.** The single most-asked feature of file managers, and you already have a recursive enumerator in `WindowsFileOperationPlanner`.

**Where.**
- `src/WinUiFileManager.Application/Abstractions/IFileSystemService.cs` — add `IObservable<long> ComputeRecursiveSize(NormalizedPath path, IScheduler, CancellationToken)`.
- Implement in `WindowsFileSystemService` using `FileSystemEnumerable<long>` with `ShouldIncludePredicate = static (ref entry) => !entry.IsDirectory`, summing `entry.Length`. Stream partial sums via `Observable.Create`.
- `FileInspectorViewModel` — on directory selection, bind the "Size" field to this observable with throttled updates.

**How.**
- Honor cancellation by re-checking the token in the enumeration loop (use a `foreach` instead of LINQ `.Sum()`).
- Publish partial sums every 500 ms; final sum on completion.

**Done when.**
- Select a directory; inspector shows "Size: computing..." → "Size: 1.2 GB".
- Switching selection cancels the running computation.
- No UI freeze during computation, even on 1M-file trees.

---

## F7. `F3` — quick compare (size + hash of selected files in both panes)

**What.** Select N items per pane. `F3` compares size and SHA-256 per matched name. Opens a result dialog listing matches / mismatches.

**Why.** Validating a build output vs. a reference folder is a daily task.

**Where.**
- New file: `src/WinUiFileManager.Application/FileOperations/CompareSelectionCommandHandler.cs`.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — new `CompareCommand`.
- `src/WinUiFileManager.Presentation/Services/WinUiDialogService.cs` — `ShowCompareResultAsync`.

**How.**
1. Walk both selections. Match by relative name (left and right are pane-local).
2. For each matched pair, first compare `Length`; if equal, compute SHA-256 via `SHA256.HashDataAsync` (or incremental if > 256 MB) off the UI thread.
3. Emit a `CompareResult` with matches, mismatches, unmatched.

**Done when.**
- F3 with no selection in the active pane does nothing.
- F3 with selections on both panes produces a result within a reasonable time.
- Cancelling the dialog aborts hashing (pass CT through).

---

## F8. Drag-drop integration (external drops in, drag-out to Explorer)

**What.**
- Drop files from Explorer into a pane — starts a copy/move based on modifier keys (Ctrl = copy, Shift = move, default = copy).
- Drag files out of a pane — Explorer accepts them as a file drop.

**Why.** Integrates with the rest of Windows without compromising the keyboard-first model.

**Where.**
- `src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml(.cs)` — handle `DragOver`, `Drop`, `DragItemsStarting`.
- `src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs` — expose a `DropItemsCommand`.

**How.**
1. `DragItemsStarting`: populate `e.Items` with `StorageFile`/`StorageFolder` instances obtained via `StorageFile.GetFileFromPathAsync` for each selected entry (use `DataPackage.SetStorageItems`).
2. `Drop`: read `DataPackageView.GetStorageItemsAsync()`, map to `NormalizedPath`, call the existing copy/move command handlers.
3. Visual affordance: highlight the pane border during `DragOver`.

**Done when.**
- Drag 5 files from Explorer into a pane → copy dialog runs.
- Select 3 files, drag to Explorer → Explorer receives them.
- Drag between panes == copy/move (same as F5/F6).

---

## F9. Symlink and hard-link creation (`Ctrl+Shift+L`)

**What.** Dialog asking the user to enter a target, offering radio buttons for Symlink / Junction / Hard link. Creates the link in the active pane.

**Why.** Developers wrangle symlinks constantly; native Explorer has no good UI for this.

**Where.**
- Add to `NativeMethods.txt`: `CreateSymbolicLinkW`, `CreateHardLinkW`.
- New: `src/WinUiFileManager.Interop/Adapters/ILinkInterop.cs`, `LinkInterop.cs`.
- New: `src/WinUiFileManager.Application/FileOperations/CreateLinkCommandHandler.cs`.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — new command.

**How.**
- Requires Windows 10 Developer Mode (or admin) for `CreateSymbolicLinkW`. Detect and surface a warning dialog if the system doesn't support it.
- Hard links are limited to files on the same volume — validate up front.

**Done when.**
- Ctrl+Shift+L opens the dialog.
- Link creation fails gracefully with a clear error when Developer Mode is off.
- The resulting link is immediately visible in the pane (via the watcher pipeline).

---

## F10. "Jump to" palette — fuzzy navigation

**What.** `Ctrl+;` opens a palette with drives + favourites + recent paths; fuzzy match navigates the active pane.

**Why.** The fastest way to jump around without using bookmarks individually.

**Where.**
- Reuses the command palette dialog from F3 with a different data source.
- New: `src/WinUiFileManager.Application/Navigation/IRecentPathsRepository.cs` + JSON implementation.

**How.**
1. Track every successful `NavigateToAsync` in a ring buffer (last 50); persist to `%LocalAppData%\WinUiFileManager\recent.json`.
2. Palette pulls drives + favourites + recent; dedupes by path.
3. Enter navigates the active pane to the chosen path.

**Done when.**
- Ctrl+; shows recent + favourites + drives.
- Selecting an entry navigates.
- Recent list survives restart and has a 50-entry cap.

---

## F11. Persistent column widths and sort — DELIVERED BY `SPEC_UI_LAYOUT_AND_RESIZING.md`

Merged into the UI layout spec, §5 (persistence). Left- and right-pane column widths, per-pane sort, inspector width, left-pane width, and main-window placement are all persisted together through the existing `PersistPaneStateCommandHandler`. This feature is closed here; track it in that spec.

---

## F12. Find-in-files (ripgrep-backed, optional)

**What.** `Ctrl+Shift+F` opens a panel that invokes `rg` over the active pane's folder and streams results to a list; clicking a result reveals the file in the opposite pane.

**Why.** Developers grep their folders constantly. No need to reimplement ripgrep.

**Where.**
- New: `src/WinUiFileManager.Presentation/Panels/FindInFilesPanel.xaml(.cs)`.
- New: `src/WinUiFileManager.Application/Search/RipgrepSearchHandler.cs`.
- `src/WinUiFileManager.Infrastructure/Services/WindowsShellService.cs` — optional helper that locates `rg.exe` on `PATH`.

**How.**
1. Detect `rg.exe` via `Process.Start` + `--version`. If missing, show "Install ripgrep to enable this feature" link.
2. Run as child process with `--json` flag; parse newline-delimited JSON from stdout via `System.Text.Json`.
3. Stream results through `ChannelReader<MatchResult>` into the panel.
4. Cancellation disposes the `Process`.

**Done when.**
- Shortcut toggles the panel.
- Search progresses live; partial results show within 100 ms for small folders.
- Esc or Stop button kills `rg`.

---

## F13. Copy-path variants (`Ctrl+Shift+C` menu)

**What.** Current `Ctrl+Shift+C` copies the absolute path. Open a flyout that offers:
- Full path (current behavior).
- Relative to opposite pane's folder.
- URI (`file:///...`).
- Hash of selection (SHA-256).

**Why.** Tiny feature; used several times a day.

**Where.**
- Extend `CopyFullPathCommandHandler` or split into `CopyPathCommandHandler` with a `CopyPathFormat` enum.
- `src/WinUiFileManager.Presentation/Views/MainShellView.xaml` — turn the toolbar button into a split button with the flyout.

**How.** All pure string manipulation; only the hash variant touches disk.

**Done when.**
- Each flyout option copies the expected string.
- Keyboard shortcut opens the flyout.

---

## F14. Status-bar toast for operation completion

**What.** When a copy/move/delete completes, flash a 3-second toast in the status bar: "Copied 42 items (120 MB) in 3.2 s".

**Why.** The current modal result dialog is heavy for small operations. A toast is unobtrusive.

**Where.**
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — emit toasts via a new `IToastService`.
- New: `src/WinUiFileManager.Presentation/Services/InlineToastService.cs`.

**How.** Add a `Toast` control to `MainShellView.xaml` status row that fades in/out on `ToastService.Show(message, timeout)`. Replace the modal `ShowOperationResultAsync` for success cases; keep the modal for failures.

**Done when.**
- Small operations show toasts.
- Failed operations still open the detailed dialog.
- Toggle to always-modal via settings (optional).

---

## F15. `Ctrl+Shift+.` — toggle hidden/system files

**What.** A keyboard toggle for the enumeration filter. Persists in settings.

**Why.** Explorer-equivalent; dev-tool users need to see `.git`, `.vs`, etc.

**Where.**
- `src/WinUiFileManager.Infrastructure/FileSystem/WindowsFileSystemService.cs` — take the `EnumerationOptions.AttributesToSkip` from a setting.
- `src/WinUiFileManager.Application/Settings/AppSettings.cs` — `ShowHiddenFiles`.
- `src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs` — toggle command.

**How.** Configure `EnumerationOptions` per call:

```csharp
var options = new EnumerationOptions
{
    AttributesToSkip = showHidden ? 0 : (FileAttributes.Hidden | FileAttributes.System),
    IgnoreInaccessible = true,
    ReturnSpecialDirectories = false,
};
```

Trigger pane reload on toggle.

**Done when.**
- Ctrl+Shift+. toggles visibility and refreshes both panes.
- State survives restart.

---

## Suggested sequencing

Deliverable-by-deliverable, aim for a release cadence of two features per sprint.

1. **Sprint A (core UX):** F1 (quick filter), F4 (reveal/terminal), F15 (hidden toggle). F11 is subsumed by `SPEC_UI_LAYOUT_AND_RESIZING.md`.
2. **Sprint B (navigation):** F5 (breadcrumb), F10 (jump-to palette), F3 (command palette).
3. **Sprint C (power features):** F6 (recursive size), F2 (tabs).
4. **Sprint D (dev-tool killers):** F7 (compare), F9 (symlinks), F13 (copy-path variants).
5. **Sprint E (polish / optional):** F8 (drag-drop), F14 (toast), F12 (ripgrep).

Each sprint should also ship the analyzer/bug tickets from the other specs — don't let bug debt accumulate behind features.

## Deferred (explicitly de-prioritized, revisit later)

These are real features on the backlog, but are intentionally de-prioritized. The agent should **not** spend effort polishing them; if they accidentally regress during unrelated work, document the regression but do not fix in-flight.

### D1. Favourites popup / flyout polish

**Status.** The favourites popup (the `MenuFlyout` on the `FavouritesAppBarButton` and its `Ctrl+D` shortcut) is a **deferred** feature. The underlying `IFavouritesRepository`, `AddFavouriteCommandHandler`, `RemoveFavouriteCommandHandler`, and `OpenFavouriteCommandHandler` remain supported; the command-bar flyout and any dedicated management surface are low priority.

**Implications for the agent.**
- Do not invest in restyling, reordering, keyboard-driven filtering, or drag-to-reorder for the flyout.
- Do not wire new analyzer/persistence work specifically for favourites UI. If a test must change, keep it minimally green.
- `MISSING_FEATURES_SPEC.md` §4 ("Favourites Management Surface") is also deferred — treat that section as paused.
- `Ctrl+D` continues to open the existing flyout; the existing flyout continues to function as-is.

**Revisit criteria.** Reopen when the human owner signals the priority change, or when user feedback highlights the popup as a friction point.

---

## Non-goals (explicitly out of scope for "low-hanging fruit")

- File preview pane (image/video/text). Requires decoding pipeline, significant surface area.
- Archive (zip/rar/7z) integration. Needs a third-party dependency (e.g., SharpCompress) and a decompression planner.
- Cloud provider deep-integration (OneDrive "keep offline/online" commands). The inspector already surfaces state; interactive pinning is a bigger design.
- Disk-usage treemap. Worthwhile but non-trivial layout algorithm; better as its own spec.
- Network share/SMB beyond what `DriveInfo` gives you.
- Multi-user / roaming settings. LocalAppData is correct for a dev tool.

Revisit any of these once the low-hanging list is delivered.
