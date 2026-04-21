# Spec: UI Layout, Resizing, and In-Place Rename

Scope: the main-shell layout, splitter behavior, in-place rename editing, and persistence of all resizable dimensions (pane widths, inspector width, per-pane column widths, sort, main-window placement). Landing order: **right after the analyzer spec**, before any other spec that changes the UI.

The review has shown that automatic end-to-end UI testing is out of scope; every change to this area is manually verified by the human owner. Consequently this spec is *prescriptive*: behaviors, widths, keyboard bindings, and persistence keys are specified exactly so the agent doesn't invent variants.

## 1. Goals

1. **Smooth, frame-paced resizing** of all splitters — no perceptible lag on a typical dev laptop (60 Hz panel, Core i5-class CPU, 4K screen). Pointer drag must feel continuous; stutter is a regression.
2. **Three splitters**:
   - Left pane ↔ Right pane (new; currently fixed 50/50).
   - Right pane ↔ Inspector (currently present but slow).
   - Both obey a minimum column width; neither has a maximum.
3. **In-place file name editing** in the active pane's grid cell, triggered by `F2` and `Shift+F6`. No modal dialog.
4. **Persisted sizes** across app sessions: left pane width, inspector width, per-pane column widths, per-pane sort column and direction, main window placement.
5. **No change to the file list virtualization.** The current `WinUI.TableView` + `ItemsRepeater` is already virtualized; this spec preserves that invariant and removes nearby anti-patterns that defeat it.

## 2. Root causes of current slowness

The review identified these contributors in `MainShellView.xaml(.cs)` and `FileInspectorView.xaml(.cs)`. Every one must be addressed.

### 2.1. Double layout pass per pointer move

`MainShellView.xaml.cs:312 OnInspectorResizePointerMoved` does:

```csharp
ViewModel.SetInspectorWidth(_inspectorResizeStartWidth + delta);  // raises PropertyChanged
UpdateInspectorLayout();                                          // sets GridLength inline
```

`OnViewModelPropertyChanged` (line 187) then fires on `InspectorWidth` and queues **another** `UpdateInspectorLayout()` through `DispatcherQueue.TryEnqueue(...)`. Every pointer move triggers two grid re-measures.

### 2.2. Inspector cascades `ContentWidth` to every category

`FileInspectorViewModel.UpdateInspectorContentWidth(double)` (line 227) loops every `Categories` and pushes `ContentWidth` on each. The inspector XAML binds `<Expander Width="{Binding ContentWidth}" …>` on every category. With 8 categories this is 8 property-changed events, 8 Expander re-measures, and the inner `ItemsRepeater` re-measures its children.

Root fix: remove the explicit width push. Let Expanders stretch via `HorizontalAlignment="Stretch"` and let the outer Grid size them. The inspector ScrollViewer already has `HorizontalScrollBarVisibility="Disabled"`, so stretching is unambiguous.

### 2.3. `SizeChanged` feedback loop on the inspector

`FileInspectorView.OnViewSizeChanged` → `UpdateInspectorContentWidth(...)` → Category `ContentWidth` change → per-category layout change → potentially re-fires `SizeChanged` on the scroll container. Even if no re-fire, this cascade runs on every pointer move.

### 2.4. Pane widths are both `*`

Current column definitions:

```
<ColumnDefinition Width="*" />       <!-- Left pane -->
<ColumnDefinition Width="2"  />      <!-- fixed border -->
<ColumnDefinition Width="*" />       <!-- Right pane -->
<ColumnDefinition Width="6" />       <!-- Inspector splitter -->
<ColumnDefinition Width="340" />     <!-- Inspector -->
```

When the Inspector column shrinks, both `*` columns (left and right panes) grow proportionally, forcing **both** panes to re-layout every pointer move. Each pane hosts a `WinUI.TableView` with 100 000 rows potentially; even with virtualization, the column-width recomputation and header re-layout are measurable.

### 2.5. `UpdateStatusBar` runs on each size cascade

`MainShellView.OnViewModelPropertyChanged` (line 191) unconditionally queues `UpdateStatusBar` on every VM property change, including `InspectorWidth`. The status bar rebuilds format strings during drag. Minor, but it runs ~120 times/second at 120Hz.

### 2.6. Pointer move is not frame-synchronized

`PointerMoved` fires at mouse/touch sample rate (often 125–500 Hz). Each sample triggers a layout pass. A 60 Hz display only benefits from ~60 layouts/sec; the rest are wasted work that still occupies the UI thread.

## 3. Target layout model

Replace the current shell grid with **absolute pixel widths** for two of three resizable regions and a single `*` column for the one that absorbs window-resize delta.

### 3.1. Column definitions

```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition x:Name="LeftPaneColumn"      Width="600" MinWidth="320" />
    <ColumnDefinition x:Name="LeftRightSplitter"   Width="6" />
    <ColumnDefinition x:Name="RightPaneColumn"     Width="*"   MinWidth="320" />
    <ColumnDefinition x:Name="InspectorSplitter"   Width="6" />
    <ColumnDefinition x:Name="InspectorColumn"     Width="340" MinWidth="260" />
</Grid.ColumnDefinitions>
```

Semantics:

- **Left pane** is an absolute pixel width. User can drag the left-right splitter to change it.
- **Right pane** is the sole `*` column. Absorbs window-resize delta and any drag of its two neighbors.
- **Inspector** is an absolute pixel width. User can drag the inspector splitter to change it.
- `MinWidth` per column prevents any region from vanishing.
- **No `MaxWidth`**. Either neighbor can grow arbitrarily as long as the right pane retains its `MinWidth`.
- When the inspector is collapsed (`IsInspectorVisible == false`), `InspectorColumn.Width` and `InspectorSplitter.Width` both become `0`.

### 3.2. Window minimum size

Set on `MainShellWindow`:

```csharp
var minSize = new global::Windows.Graphics.SizeInt32(
    320 + 6 + 320 + 6 + 260,  // sum of MinWidths
    480);
appWindow.Resize(/* restored size */);
// Clamp min size using AppWindow.SetPresenter(OverlappedPresenter) min dimensions,
// available on Windows App SDK 1.6+. If not present on the current SDK, enforce
// in a SizeChanged handler that calls Resize back to the min.
```

If the SDK in use doesn't expose a native min-size API, enforce in `SizeChanged` by resizing back. This is a small XAML island quirk worth a comment.

### 3.3. Splitter widget

Use **`CommunityToolkit.WinUI.Controls.Sizer`** (new NuGet; see `SPEC_NUGET_MODERNIZATION.md` §3). Reasons:

- Built for exactly this scenario — a resize thumb between grid columns.
- Two-phase resize: a "ghost" preview during drag, commit on release — no continuous layout churn during drag.
- Handles pointer-capture, cursor, and accessibility announcements for free.
- Supports `Orientation="Vertical"` for column-splitter use (confusingly named; "Vertical" means a vertical line between horizontal neighbors).

Replace the hand-rolled `Border` + `PointerPressed/Moved/Released` handlers. The hand-rolled code in `MainShellView.xaml.cs:297-353` is deleted.

Wire-up:

```xml
<controls:Sizer
    Grid.Column="1"
    Orientation="Vertical"
    Width="6"
    HorizontalAlignment="Stretch"
    ManipulationMode="TranslateX"
    ChangeEvent="OnLeftRightSplitterChange" />
```

The Sizer emits a `ChangeEvent(double horizontalChange)` on commit. The handler updates `LeftPaneColumn.Width` and persists after debounce (see §5).

If `CommunityToolkit.WinUI.Controls.Sizer` is not acceptable or unavailable in the target SDK, fall back to a hand-rolled splitter that follows §3.4 — but the default must be the Toolkit control.

### 3.4. Hand-rolled splitter (fallback only)

If the Sizer can't be used, the fallback splitter must:

1. Capture the pointer on `PointerPressed`; release on `PointerReleased` and `PointerCaptureLost`.
2. Throttle `PointerMoved` updates to one per display frame using a `DispatcherQueueTimer` with `IsRepeating = true, Interval = TimeSpan.FromMilliseconds(16)`, or by flipping a `_pendingDelta` variable consumed from `CompositionTarget.Rendering`.
3. Apply the delta directly to `LeftPaneColumn.Width` (or `InspectorColumn.Width`) — no VM round-trip during drag.
4. Commit to the VM only on `PointerReleased`, then persist (§5).
5. Never set `IsChecked`, `Visibility`, or any other unrelated property in the move path.

## 4. Per-move fixes

Even with a better splitter widget, the other layout cascades must be cleaned up.

### 4.1. Stop cascading `ContentWidth` to inspector categories

In `FileInspectorViewModel`:

- Remove `InspectorContentWidth` observable property.
- Remove `UpdateInspectorContentWidth(double)`.
- Remove `ContentWidth` from `FileInspectorCategoryViewModel`.

In `FileInspectorView.xaml`:

- Drop `<Grid Width="{Binding InspectorContentWidth}" …>` wrapping the category repeater — stretch naturally.
- Drop `Width="{Binding ContentWidth}"` on the per-category Expander; use `HorizontalAlignment="Stretch"` and `HorizontalContentAlignment="Stretch"`.

In `FileInspectorView.xaml.cs`:

- Remove `OnViewSizeChanged` subscription and the helper `GetInspectorContentWidth()`.

The inspector's natural layout (ScrollViewer → Grid → ItemsRepeater of Expanders) already stretches horizontally when given a constrained width. The cascade was load-bearing for nothing.

### 4.2. Remove the inline `UpdateInspectorLayout` call

After the Sizer lands (or the fallback with frame throttling), the pointer-moved handler no longer exists. Keep `UpdateInspectorLayout` only as the VM-driven reaction to `IsInspectorVisible` changes. Specifically:

- `OnViewModelPropertyChanged` reacts to `IsInspectorVisible` only (not `InspectorWidth`).
- `InspectorWidth` is applied by binding `InspectorColumn.Width` directly through a `GridLengthConverter` (one-way, VM → column). No code-behind.

Binding:

```xml
<ColumnDefinition x:Name="InspectorColumn"
                  Width="{x:Bind ViewModel.InspectorWidth,
                                 Mode=OneWay,
                                 Converter={StaticResource PixelGridLengthConverter}}"
                  MinWidth="260" />
```

Add a `PixelGridLengthConverter` (tiny utility class) in `WinUiFileManager.Presentation/Converters/`. `GridUnitType.Pixel`.

### 4.3. Status bar does not update during drag

`OnViewModelPropertyChanged` in `MainShellView.xaml.cs:187` currently re-enters `UpdateStatusBar` for every property change. Narrow it:

```csharp
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    DispatcherQueue.TryEnqueue(() =>
    {
        switch (e.PropertyName)
        {
            case nameof(MainShellViewModel.ActivePane):
                UpdateStatusBar();
                UpdateActivePaneBorders();
                break;
            case nameof(MainShellViewModel.IsInspectorVisible):
                UpdateInspectorLayout();
                break;
            // InspectorWidth is handled by XAML binding; no code-behind.
        }
    });
}
```

Status bar itself is already addressed in `SPEC_BUG_FIXES.md` P1.5/B3 — keep coordination with that work.

### 4.4. Pane-width change does not trigger list reflow

The new layout makes only the right pane's `*` column re-measure when either splitter moves. The right pane's `TableView` must not re-measure all items on width change — confirm by:

- Profiling with `dotnet-trace` during a drag: the `MainShellView.Arrange` and `TableView.Arrange` should occupy the thread, **not** `TableView.Measure` for every row.
- If row `Measure` calls appear at the per-row scale, the virtualization has been broken. Causes to rule out: a `ScrollViewer` ancestor with `VerticalScrollBarVisibility="Disabled"` forcing the list to measure to infinite height, or `Auto` sizing on an ancestor.

The current XAML uses `RowDefinition Height="*"` around the TableView — correct. Keep it.

## 5. Persistence

Extend `AppSettings` (`src/WinUiFileManager.Application/Settings/AppSettings.cs`) with:

```csharp
public double LeftPaneWidth { get; init; } = 600d;
// InspectorWidth already present.
public PaneColumnLayout LeftPaneColumns { get; init; } = PaneColumnLayout.Default;
public PaneColumnLayout RightPaneColumns { get; init; } = PaneColumnLayout.Default;
public SortState LeftPaneSort { get; init; } = SortState.Default;
public SortState RightPaneSort { get; init; } = SortState.Default;
public WindowPlacement MainWindowPlacement { get; init; } = WindowPlacement.Default;
```

New domain types (each in its own file per `CODING_STYLE.md`):

- `PaneColumnLayout` — a readonly struct holding `(double NameWidth, double ExtensionWidth, double SizeWidth, double ModifiedWidth, double AttributesWidth)`. Each defaults to the current XAML width; agent preserves the XAML widths as initial values.
- `SortState` — `(SortColumn Column, bool Ascending)`.
- `WindowPlacement` — `(int X, int Y, int Width, int Height, bool IsMaximized)`.

Persistence flow:

- **During drag:** splitter commits to VM on `PointerReleased`. Column resize commits when the user releases the column header handle (TableView fires a column-resize-ended event; if not, bind to the column's `SizeChanged` with a 250 ms debounce).
- **On window move/resize:** debounce 250 ms via an Rx `Throttle` in `MainShellWindow.xaml.cs`; the VM stores last observed placement in memory.
- **On app exit:** `MainShellWindow.OnAppWindowClosing` calls `PersistStateAsync`. `PersistPaneStateCommandHandler` writes the whole `AppSettings` atomically (JSON repo already handles this).

**No eager persistence during drag.** Every drag is O(1) in persisted-state writes: one commit per drag.

Restoration flow on `MainShellViewModel.InitializeAsync`:

1. Load `AppSettings` (already happens).
2. Apply `InspectorWidth`, `LeftPaneWidth` to VM observable properties — XAML bindings propagate to column widths.
3. Apply column layouts to each `FilePaneViewModel` (new `SetColumnLayout(...)` method that updates the TableView columns via the shared sync object `FilePaneTableSortSync` — add `SyncColumnWidths(TableView, PaneColumnLayout)`).
4. Apply sort states; existing `FilePaneViewModel.SetSort(...)` handles this.
5. Apply window placement via `AppWindow.MoveAndResize(...)` and `OverlappedPresenter.Maximize()` if `IsMaximized`.

Fallbacks if persisted values are out of range (e.g., monitor disconnected): clamp to primary display bounds; window placement off-screen resets to center.

## 6. In-place rename

### 6.1. Trigger surfaces

| Trigger | Existing | New |
|---|---|---|
| `Shift+F6` | Opens modal rename dialog | Enters in-cell edit mode on current item |
| `F2` | No-op | Enters in-cell edit mode on current item |
| Toolbar "Rename" button | Opens modal | Enters in-cell edit mode |
| Click on selected name (not a double-click) | No-op | Out of scope for v1 — keyboard-first; ignore |

The modal `ShowRenameDialogAsync` and its `ContentDialog` are **removed**. `IDialogService.ShowRenameDialogAsync` is deleted. Any test that exercises it is updated to drive in-cell edit.

### 6.2. Model changes

Add to `FileEntryViewModel`:

```csharp
[ObservableProperty]
public partial bool IsEditing { get; set; }

[ObservableProperty]
public partial string EditBuffer { get; set; } = string.Empty;
```

`IsEditing` starts `false`. `EditBuffer` is initialized from `Name` when edit begins, cleared on commit/cancel.

### 6.3. Cell template

The Name column in `FileEntryTableView.xaml` becomes a `TableViewTemplateColumn`:

```xml
<tv:TableViewTemplateColumn Header="Name" SortMemberPath="Name"
                            MinWidth="100" Width="{x:Bind ...}">
    <tv:TableViewTemplateColumn.CellTemplate>
        <DataTemplate x:DataType="vm:FileEntryViewModel">
            <Grid>
                <TextBlock Text="{x:Bind Name}"
                           Style="{StaticResource EllipsisTextCellStyle}"
                           Visibility="{x:Bind IsEditing,
                                               Mode=OneWay,
                                               Converter={StaticResource InverseBoolToVisibility}}" />
                <TextBox x:Name="NameEditor"
                         Text="{x:Bind EditBuffer, Mode=TwoWay,
                                        UpdateSourceTrigger=PropertyChanged}"
                         Visibility="{x:Bind IsEditing,
                                             Mode=OneWay,
                                             Converter={StaticResource BoolToVisibility}}"
                         KeyDown="OnNameEditorKeyDown"
                         LostFocus="OnNameEditorLostFocus" />
            </Grid>
        </DataTemplate>
    </tv:TableViewTemplateColumn.CellTemplate>
</tv:TableViewTemplateColumn>
```

Two converters (add to `Converters/`): `BoolToVisibility` and `InverseBoolToVisibility`.

### 6.4. Enter / exit edit mode

New methods on `FilePaneViewModel`:

```csharp
public void BeginRenameCurrent()
{
    var current = CurrentItem;
    if (current is null || current.IsParentEntry || IsLoading) return;
    // exactly one item may be editing at a time
    foreach (var item in _sortedItems) { item.IsEditing = false; }
    current.EditBuffer = current.Name;
    current.IsEditing = true;
}

public async Task CommitRenameAsync(FileEntryViewModel entry, CancellationToken ct)
{
    if (!entry.IsEditing) return;
    var newName = entry.EditBuffer.Trim();
    var oldName = entry.Name;
    entry.IsEditing = false;
    if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
    try
    {
        var summary = await _renameHandler.ExecuteAsync(entry.Model, newName, ct);
        // on success, the watcher will emit a Rename event; the SourceCache
        // replaces the entry by its UniqueKey. Log-level feedback only.
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Rename failed");
        ErrorMessage = ex.Message;
    }
}

public void CancelRename(FileEntryViewModel entry)
{
    entry.IsEditing = false;
    entry.EditBuffer = string.Empty;
}
```

Note: `RenameHandler` stays command-layer but no longer depends on a dialog. Its signature is already `ExecuteAsync(FileSystemEntryModel model, string newName, CancellationToken ct)`. `MainShellViewModel.RenameAsync` becomes a thin call to `ActivePane.BeginRenameCurrent()`.

### 6.5. Keyboard bindings in the cell

`FileEntryTableView.xaml.cs` handlers:

```csharp
private async void OnNameEditorKeyDown(object sender, KeyRoutedEventArgs e)
{
    if (sender is not TextBox tb || tb.DataContext is not FileEntryViewModel entry)
        return;
    switch (e.Key)
    {
        case VirtualKey.Enter:
            e.Handled = true;
            var host = GridViewModel.Host;
            if (host is not null)
                await host.CommitRenameAsync(entry, CancellationToken.None);
            FileTable.Focus(FocusState.Keyboard);
            break;
        case VirtualKey.Escape:
            e.Handled = true;
            GridViewModel.Host?.CancelRename(entry);
            FileTable.Focus(FocusState.Keyboard);
            break;
    }
}

private async void OnNameEditorLostFocus(object sender, RoutedEventArgs e)
{
    if (sender is not TextBox tb || tb.DataContext is not FileEntryViewModel entry)
        return;
    var host = GridViewModel.Host;
    if (host is not null && entry.IsEditing)
    {
        await host.CommitRenameAsync(entry, CancellationToken.None);
    }
}
```

In `OnPreviewKeyDown`, add `F2` → `host.BeginRenameCurrent(); e.Handled = true;`. Leave existing `Shift+F6` path working (either keep the path via `MainShellViewModel.RenameCommand` which now calls `BeginRenameCurrent`, or route both keys to the same handler).

### 6.6. Focus and selection

When `IsEditing` flips true:

1. The Name column's `TextBox` becomes visible.
2. After one dispatcher tick (so the template has instantiated), focus the `TextBox` with `FocusState.Keyboard`.
3. Select the file **stem** (not the extension) — the common rename case edits the stem. Compute stem length from `Path.GetExtension(Name)`.
4. If the user clicks elsewhere (LostFocus), commit.

Implementation: subscribe to `FileEntryViewModel.IsEditing` change via a small attached behavior or a `FrameworkElement.Loaded` handler on the TextBox that calls `Focus` and `Select(0, stemLength)` once per transition.

### 6.7. Validation

- Empty/whitespace → treat as cancel (no filesystem call).
- Unchanged → treat as cancel.
- Invalid characters (`\ / : * ? " < > |`) → show the entry's `ErrorMessage` on the pane and **keep edit mode open** so the user can fix; flash the TextBox red via `VisualState`.
- Collision (destination exists) → same — let the rename handler return failure and surface the message without closing edit mode.
- Successful rename: watcher emits `Renamed`; the pane's `SourceCache` replaces by `UniqueKey`. Focus returns to the row for the new entry.

### 6.8. Watcher interaction

`WindowsDirectoryChangeStream.OnRenamed` (line 136) emits a `DirectoryChange` with `OldPath` and new `Path`. `FilePaneViewModel.BuildWatcherBatch` already handles renames by removing the old path key and adding the new one. No new logic required; verify the new entry receives focus post-commit.

## 7. Interactions with other specs

- **`SPEC_FEATURE_LOW_HANGING_FRUIT.md` F11** (Persistent column widths and sort) is **delivered by this spec**. Mark it done.
- **`SPEC_BUG_FIXES.md` P1.5 / B3 / UpdateStatusBar batching** coordinates here — both specs converge on removing property-change storms during drag.
- **`SPEC_LONG_PATHS.md` capability gating** applies to rename: long paths support `RenameEntry` in the matrix, so in-cell rename must work on long paths too. Confirm in the acceptance list.
- **`SPEC_NUGET_MODERNIZATION.md`** adds `CommunityToolkit.WinUI.Controls.Sizer` for §3.3.
- **`winui-file-manager-keyboard-shortcuts-spec.md` §12.9** ("Rename in place") already permits inline or dialog; this spec tightens to *inline only*. Update the referenced section to remove the dialog alternative.
- **`MISSING_FEATURES_SPEC.md` §3** (persistence) is delivered by this spec.
- **`SPEC_KEYBOARD_SHORTCUTS_GAPS.md`** cross-references §6.1 for `F2`.

## 8. Manual verification checklist

Because automated UI testing is out of scope, every change here lands with a manual checklist the agent is required to self-exercise before handing off. Take screenshots where noted.

### 8.1. Splitter smoothness

- [ ] Grab the **left-right splitter**. Drag slowly 5 px at a time across 500 px. The left pane width tracks the pointer. No stutter on a 60 Hz display.
- [ ] Grab the **inspector splitter**. Same test.
- [ ] Drag each splitter at maximum pointer speed. The layout keeps up; no lag that exceeds one frame (~16 ms).
- [ ] Open a 100 000-file folder (use `powershell/create-test-folder.ps1` / equivalent). Drag each splitter. Smoothness unchanged.
- [ ] With the file list scrolled halfway down a 100 000-file folder, drag a splitter. The scroll position does not jump.

### 8.2. Column minimums

- [ ] Drag the left-right splitter all the way left. The left pane stops at its `MinWidth = 320`.
- [ ] Drag the inspector splitter all the way right. The inspector stops at its `MinWidth = 260`.
- [ ] Both splitters at their minimums simultaneously: the right pane still has its `MinWidth = 320`. No column collapses.

### 8.3. Persistence

- [ ] Resize each splitter to non-default widths. Resize two pane columns (e.g., Name → 400 px, Modified → 140 px). Change the sort on the left pane to "Size descending". Close the app (Alt+F4). Reopen. All widths, sort, and window placement restored.
- [ ] On a machine with the app's primary monitor disconnected (move settings file to a machine with a smaller screen), reopen. Window appears centered on the primary display, not off-screen.

### 8.4. In-place rename

- [ ] Navigate to a test folder. Press `F2` on a file. The Name cell transitions to a TextBox; the **file stem** (not extension) is selected.
- [ ] Type a new stem; press `Enter`. The file is renamed. Focus returns to the row; the renamed item is the sole selection and remains the current item.
- [ ] Press `F2`, type a new name, press `Esc`. No rename. The original name is displayed.
- [ ] Press `F2`, click elsewhere (outside the editor). Rename commits. Focus leaves edit mode.
- [ ] Press `F2`, type a name containing `\`. No filesystem call occurs; the TextBox shows red / keeps edit mode.
- [ ] Press `F2`, type a name that already exists in the folder. Error surfaces; edit mode stays open; user can fix.
- [ ] Press `Shift+F6` — same behavior as `F2`.
- [ ] Press `F2` on the `..` parent entry. Nothing happens.
- [ ] Toolbar "Rename" button triggers the same in-cell edit.
- [ ] Rename a file inside a folder whose path is 400 chars (long path). In-cell rename works; capability policy does not disable it.

### 8.5. No modal rename remains

- [ ] `grep` the codebase for `ShowRenameDialogAsync` — zero results.
- [ ] `grep` for `"Rename"` in XAML `ContentDialog` instances — zero results in `Presentation/Services/WinUiDialogService.cs` (the method is deleted).

### 8.6. Inspector still renders correctly

- [ ] Select a file; all inspector categories render. Category widths stretch to the inspector column width.
- [ ] Resize the inspector; all expanders resize smoothly. No blank gaps, no clipping.
- [ ] Select 0 files; the inspector empty state renders.
- [ ] Select 2+ files; the inspector shows its multi-select state (currently: empty).

### 8.7. Regression — existing keyboard flows

- [ ] Every shortcut in `winui-file-manager-keyboard-shortcuts-spec.md` §17 still behaves as documented.
- [ ] Pane switch (`Tab`), path box focus (`Ctrl+L`), copy/move/delete shortcuts still work.

## 9. Acceptance

This spec is complete when:

- All items in §8 pass on a Windows 11 machine at 100% DPI and at 150% DPI.
- `MainShellView.xaml.cs` no longer contains manual splitter `PointerPressed/Moved/Released` handlers (both are delegated to the Sizer or to the fallback with frame throttling).
- `UpdateInspectorLayout` is no longer called per pointer move.
- `FileInspectorViewModel.InspectorContentWidth` and `FileInspectorCategoryViewModel.ContentWidth` no longer exist.
- `IDialogService.ShowRenameDialogAsync` no longer exists; `WinUiDialogService` does not instantiate a rename `ContentDialog`.
- `AppSettings` persists left-pane width, inspector width, per-pane column layout, per-pane sort, and main-window placement.
- First-launch (no settings file) uses the defaults in §3.1 and §5.
- Feature spec F11 is marked delivered.

## 10. Non-goals

- Horizontal scrolling in either pane. The pane `MinWidth` prevents the list from becoming unusably narrow.
- Dockable / undockable inspector. Inspector remains a right-side column; visibility toggles with `Ctrl+I`.
- Drag-and-drop reordering of columns. Out of scope for v1.
- Multi-select rename (batch rename). Out of scope.
- Undo/redo of rename. Use the OS recycle bin model or see the broader feature roadmap.
