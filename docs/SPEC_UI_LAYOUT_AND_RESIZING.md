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

The review identified these contributors in `MainShellView.xaml(.cs)` and `FileInspectorView.xaml(.cs)`. Each subsection below names a root cause **and** the prescribed fix, with a pointer at the §3 / §4 / §5 section that carries the full prescription and its current shipping status. An implementing agent does not have to choose an approach — every fix is named.

### 2.1. Double layout pass per pointer move

`MainShellView.xaml.cs:312 OnInspectorResizePointerMoved` does:

```csharp
ViewModel.SetInspectorWidth(_inspectorResizeStartWidth + delta);  // raises PropertyChanged
UpdateInspectorLayout();                                          // sets GridLength inline
```

`OnViewModelPropertyChanged` (line 187) then fires on `InspectorWidth` and queues **another** `UpdateInspectorLayout()` through `DispatcherQueue.TryEnqueue(...)`. Every pointer move triggers two grid re-measures.

**Fix (see §3.3, §4.2, §4.3; shipped in U-1 / U-2 with later drag-polish).** Delete the old hand-rolled resize math and the `UpdateInspectorLayout` invocation from the move path. `InspectorColumn.Width` is bound from `MainShellViewModel.InspectorWidth` through `PixelGridLengthConverter` (see §4.2); the `CommunityToolkit.WinUI.Controls.GridSplitter` from §3.3 drives width changes directly against the column. `OnViewModelPropertyChanged` reacts only to `ActivePane` / `IsInspectorVisible`, never `InspectorWidth` (see §4.3). The shipped implementation also adds lightweight splitter pointer-start / pointer-end handlers that **do not compute widths**; they temporarily freeze both `FileTable` controls to their current width during drag, then release them back to auto width afterward. Net result: one layout pass per width change, no hand-rolled resize algorithm in the move path, and no drag-time table-width thrash.

### 2.2. Inspector cascades `ContentWidth` to every category

`FileInspectorViewModel.UpdateInspectorContentWidth(double)` (line 227) loops every `Categories` and pushes `ContentWidth` on each. The inspector XAML binds `<Expander Width="{Binding ContentWidth}" …>` on every category. With 8 categories this is 8 property-changed events, 8 Expander re-measures, and the inner `ItemsRepeater` re-measures its children.

**Fix (see §4.1; shipped in U-2).** Delete `FileInspectorViewModel.InspectorContentWidth` and `UpdateInspectorContentWidth(double)`; delete `FileInspectorCategoryViewModel.ContentWidth`; drop the `<Grid Width="{Binding InspectorContentWidth}" …>` wrapper and the per-Expander `Width="{Binding ContentWidth}"` binding. Let each `Expander` stretch via `HorizontalAlignment="Stretch"` + `HorizontalContentAlignment="Stretch"` under the inspector `ScrollViewer` (which already has `HorizontalScrollBarVisibility="Disabled"`, so the stretch width is unambiguous).

### 2.3. `SizeChanged` feedback loop on the inspector

`FileInspectorView.OnViewSizeChanged` → `UpdateInspectorContentWidth(...)` → Category `ContentWidth` change → per-category layout change → potentially re-fires `SizeChanged` on the scroll container. Even if no re-fire, this cascade runs on every pointer move.

**Fix (see §4.1; shipped in U-2).** Remove the `SizeChanged` subscription in `FileInspectorView.xaml.cs` along with `OnViewSizeChanged` and the `GetInspectorContentWidth()` helper. The cascade was load-bearing for nothing — the ScrollViewer/Grid/ItemsRepeater tree stretches naturally once §2.2's fix lands.

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

**Fix (see §3.1; shipped in U-1).** Replace the 5-column layout with pixel widths on the side columns and a single `*` column in the middle:

```xml
<ColumnDefinition x:Name="LeftPaneColumn"    Width="{Binding LeftPaneWidth,   Converter={StaticResource PixelGridLengthConverter}}" MinWidth="320" />
<ColumnDefinition x:Name="LeftRightSplitter" Width="6" />
<ColumnDefinition x:Name="RightPaneColumn"   Width="*"                                                                              MinWidth="320" />
<ColumnDefinition x:Name="InspectorSplitter" Width="6" />
<ColumnDefinition x:Name="InspectorColumn"   Width="{Binding InspectorWidth,  Converter={StaticResource PixelGridLengthConverter}}" MinWidth="260" />
```

`LeftPaneColumn` and `InspectorColumn` are pixel widths bound one-way through `PixelGridLengthConverter` from VM properties. `RightPaneColumn` is the sole `*` — it absorbs every window-resize and splitter-drag delta, so only **one** pane (the right one) needs a layout pass per move instead of both.

### 2.5. `UpdateStatusBar` runs on each size cascade

`MainShellView.OnViewModelPropertyChanged` (line 191) unconditionally queues `UpdateStatusBar` on every VM property change, including `InspectorWidth`. The status bar rebuilds format strings during drag. Minor, but it runs ~120 times/second at 120Hz.

**Fix (see §4.3; shipped in U-1).** Narrow `OnViewModelPropertyChanged` to an explicit `switch` that handles only `ActivePane` (→ `UpdateStatusBar` + `UpdateActivePaneBorders`) and `IsInspectorVisible` (→ `UpdateInspectorLayout`). `InspectorWidth` is no longer observed in code-behind — the XAML column binding in §2.4's fix handles it. Status-bar rebuilds are decoupled from drag.

### 2.6. Pointer move is not frame-synchronized

`PointerMoved` fires at mouse/touch sample rate (often 125–500 Hz). Each sample triggers a layout pass. A 60 Hz display only benefits from ~60 layouts/sec; the rest are wasted work that still occupies the UI thread.

**Fix (see §3.3; shipped in U-1 + later drag-polish).** Use `CommunityToolkit.WinUI.Controls.GridSplitter` with `ResizeBehavior="PreviousAndNext"`, `ResizeDirection="Columns"`, and `DragIncrement="8"` / `KeyboardIncrement="8"`. The control handles pointer-capture, cursor state, and accessibility. `DragIncrement` quantizes width updates to 8 px steps so sub-frame pointer samples coalesce before the column is re-written. The shipped implementation supplements `GridSplitter` with pointer-start / pointer-end hooks that freeze both `FileTable` controls during drag and release them on pointer end. Those hooks are allowed because they do not implement resize math; `GridSplitter` remains the sole control that owns column resizing.

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

Use **`CommunityToolkit.WinUI.Controls.GridSplitter`** from the `CommunityToolkit.WinUI.Controls.Sizers` NuGet (see `SPEC_NUGET_MODERNIZATION.md` §3). Reasons:

- Built for exactly this scenario — a resize thumb between grid columns.
- Handles pointer-capture, cursor, and accessibility announcements for free.
- `ResizeBehavior="PreviousAndNext"` / `ResizeDirection="Columns"` gives the desired "adjust the two neighboring columns" semantics without code-behind.
- `DragIncrement` and `KeyboardIncrement` expose the resize step for both mouse and keyboard-driven resizing.

The `Sizers` package also exports a `Sizer` control. Either works; the implementation uses `GridSplitter` because its `ResizeBehavior` enum matches the 5-column shell layout cleanly (left and right neighbors share the delta). Both controls share the same package reference, so spec 2's N-2b batch (`CommunityToolkit.WinUI.Controls.Sizers`) satisfies this section regardless of which control is picked.

Replace the old hand-rolled `Border` + `PointerPressed/Moved/Released` resize handlers. `MainShellView.xaml.cs` does not own splitter delta math. The shipped implementation does keep small pointer-start / pointer-end hooks around the `GridSplitter` instances so both `FileTable` controls can freeze to `ActualWidth` during drag and rejoin normal layout on release. That freeze path is a deliberate performance mitigation and is part of the accepted design.

Wire-up (as shipped):

```xml
<toolkit:GridSplitter
    Grid.Column="1"
    Background="{ThemeResource SystemControlBackgroundBaseLowBrush}"
    DragIncrement="8"
    KeyboardIncrement="8"
    ResizeBehavior="PreviousAndNext"
    ResizeDirection="Columns" />
```

The column widths are bound via `PixelGridLengthConverter` on `LeftPaneColumn` and `InspectorColumn`; `GridSplitter` updates those columns in place during drag. During a splitter drag, the view freezes both `FileTable` controls to their `ActualWidth` and left-aligns them so shell resizing does not continuously reflow the table chrome; on drag end, both return to normal auto width. Persistence is debounced via the window's close handler (see §5) — there is no per-drag write.

**Do not implement a hand-rolled splitter.** `GridSplitter` remains the only surface that changes column widths. Lightweight pointer-start / pointer-end hooks that freeze and release the file tables are explicitly allowed; they are not a substitute splitter implementation. If `GridSplitter` itself appears to misbehave (e.g., keeps the CPU busy, drops events, or does not emit a drag-commit event), **stop and raise the issue** in the batch's handoff note under "Surprises" per `SPEC_AGENT_BATCHING_PLAN.md` §6. If the Toolkit control is genuinely unworkable, the human owner will pick a replacement (likely `Microsoft.UI.Xaml.Controls.GridSplitter` or a different package), not the agent.

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

With `GridSplitter` driving resizes (§3.3), the pointer-moved handler no longer exists. Keep `UpdateInspectorLayout` only as the VM-driven reaction to `IsInspectorVisible` changes. Specifically:

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

### 4.3. Status bar: delete `UpdateStatusBar` and move to XAML bindings

There is no drag-time status-bar update to fix. The status bar fires on `ActivePane` change (and on any active-pane `PropertyChanged` — `ItemCount`, `SelectedCount`, `CurrentPath`, `IncrementalSearchText`), not on splitter drag. `InspectorWidth` is already not observed by `OnViewModelPropertyChanged`. The real cleanup here is to **remove the status-bar code-behind entirely** and bind it from XAML.

**Fix.**

1. Move the composed strings from `MainShellView.UpdateStatusBar` into computed read-only properties on `FilePaneViewModel`:
   - `PaneLabel` — `"Left"` or `"Right"` from `PaneId`.
   - `ItemCountDisplay` — `"<N> items"`, with `" | Search: <x>"` appended when `IncrementalSearchText` is non-empty.
   - `SelectedDisplay` — `"<N> selected"`, with `" (<formatted bytes>)"` appended when `SelectedCount > 0` and total size > 0.
   Use `[NotifyPropertyChangedFor]` on the triggering `[ObservableProperty]` fields so `PropertyChanged` on these display strings fires only when the inputs change.
2. Add `MainShellViewModel.ActivePaneLabel` returning `"<ActivePane.PaneLabel> active"` (or keep the concatenation in XAML with `StringFormat`).
3. Rewrite the status-bar row in `MainShellView.xaml` to bind each `TextBlock` directly:
   ```xml
   <TextBlock x:Name="ActivePaneText"  Text="{x:Bind ViewModel.ActivePaneLabel,            Mode=OneWay}" />
   <TextBlock x:Name="PathText"        Text="{x:Bind ViewModel.ActivePane.CurrentPath,     Mode=OneWay}" />
   <TextBlock x:Name="ItemCountText"   Text="{x:Bind ViewModel.ActivePane.ItemCountDisplay,Mode=OneWay}" />
   <TextBlock x:Name="SelectedText"    Text="{x:Bind ViewModel.ActivePane.SelectedDisplay, Mode=OneWay}" />
   ```
4. Delete, from `MainShellView.xaml.cs`:
   - `UpdateStatusBar()` and the initial call in the constructor.
   - `OnPanePropertyChanged(object?, PropertyChangedEventArgs)` and the two `PropertyChanged += OnPanePropertyChanged` subscriptions on `LeftPane` / `RightPane`.
   - The `case nameof(MainShellViewModel.ActivePane): UpdateStatusBar();` line — `UpdateActivePaneBorders` stays (it's visual, not data).
   - `FormatByteSize(long)` — moves into the VM alongside `SelectedDisplay`.
5. Net effect: zero code-behind subscriptions for the status bar. The status text reacts through the standard XAML binding path only. No more possibility of a stray `UpdateStatusBar` invocation firing from an unrelated property change.

Tests: the three new VM properties each get one unit test per case (empty, single-item, multi-select with size, with / without incremental search). No view-layer tests.

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

Persistence flow — **on app exit only**, for simplicity:

- **During the session:** the VM holds current widths, column layouts, sort, and window placement in memory. `LeftPaneWidth` / `InspectorWidth` update as the user drags `GridSplitter` (bound two-way through `PixelGridLengthConverter`); `PaneColumnLayout`, `SortState`, and `MainWindowPlacement` are captured once, on exit.
- **On app exit:** `MainShellWindow.OnAppWindowClosing`:
  1. Calls `ShellView.CapturePaneColumnLayouts()` to read each `TableView`'s current column `ActualWidth`s into the pane VMs.
  2. Snapshots `AppWindow.Position` / `AppWindow.Size` and the maximized flag into `_viewModel.MainWindowPlacement`.
  3. Awaits `PersistStateAsync()`, which builds a `PersistPaneStateRequest` from the VM state and hands it to `PersistPaneStateCommandHandler`. The JSON repo writes the whole `AppSettings` atomically.

No debounce, no `Throttle`, no per-drag writes, no `SizeChanged` subscribers on columns, no window-move observers. One write per session. If the app crashes before exit, the session's resize / sort / move is lost — accepted tradeoff for simplicity.

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

The Name column in `FileEntryTableView.xaml` is a `TableViewTemplateColumn` with both a read-only `CellTemplate` and an `EditingTemplate`. The control itself toggles between the two when a cell enters / leaves edit mode, so no `IsEditing`-driven `Visibility` converter is needed.

```xml
<tv:TableViewTemplateColumn
    x:Name="NameColumn"
    Header="Name"
    SortMemberPath="Name"
    MinWidth="100"
    Width="320"
    IsReadOnly="False"
    CanSort="True">
    <tv:TableViewTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}"
                       Style="{StaticResource EllipsisTextCellStyle}" />
        </DataTemplate>
    </tv:TableViewTemplateColumn.CellTemplate>
    <tv:TableViewTemplateColumn.EditingTemplate>
        <DataTemplate>
            <TextBox Text="{Binding EditBuffer, Mode=TwoWay,
                                                 UpdateSourceTrigger=PropertyChanged}"
                     VerticalContentAlignment="Center"
                     IsSpellCheckEnabled="False" />
        </DataTemplate>
    </tv:TableViewTemplateColumn.EditingTemplate>
</tv:TableViewTemplateColumn>
```

The TableView events `BeginningEdit` / `PreparingCellForEdit` / `CellEditEnding` / `CellEditEnded` replace the hand-rolled `KeyDown` / `LostFocus` handlers. `BoolToVisibility` / `InverseBoolToVisibility` converters are *not* required and must not be added — the `EditingTemplate` swap is the supported pattern.

### 6.4. Enter / exit edit mode

`FilePaneViewModel` exposes the following surface:

```csharp
// Private sentinel so the view can ignore edit events for rows the VM did not choose.
private FileEntryViewModel? _activeEditingEntry;
public FileEntryViewModel? ActiveEditingEntry => _activeEditingEntry;

// Raised when BeginRenameCurrent promotes an entry to editing. The view subscribes
// and starts the cell editor; the VM never calls into the view directly.
public event EventHandler<FileEntryViewModel>? RenameRequested;

public void BeginRenameCurrent()
{
    if (IsLoading) return;
    var current = CurrentItem;
    if (current is null || current.IsParentEntry) return;

    if (_activeEditingEntry is not null && !ReferenceEquals(_activeEditingEntry, current))
        CancelRename(_activeEditingEntry);

    ErrorMessage = null;
    current.EditBuffer = current.Name;
    current.IsEditing = true;
    _activeEditingEntry = current;
    RenameRequested?.Invoke(this, current);
}

public async Task<bool> CommitRenameAsync(
    FileEntryViewModel entry,
    string? candidateName,
    CancellationToken ct)
{
    if (!ReferenceEquals(_activeEditingEntry, entry) || !entry.IsEditing)
        return false;

    var newName = (candidateName ?? entry.EditBuffer).Trim();
    entry.EditBuffer = candidateName ?? entry.EditBuffer;
    ErrorMessage = null;

    if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, entry.Name, StringComparison.Ordinal))
    {
        CancelRename(entry);
        return true;
    }

    if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        return false; // keep edit mode open; caller re-focuses the editor

    try
    {
        var summary = await _renameHandler.ExecuteAsync(entry.Model, newName, ct);
        if (summary.FailedCount == 0 && summary.Status == OperationStatus.Succeeded)
        {
            entry.IsEditing = false;
            entry.EditBuffer = string.Empty;
            _activeEditingEntry = null;
            return true;
        }

        _logger.LogDebug("Inline rename rejected for {Path}: {Message}",
            entry.Model.FullPath.DisplayPath, summary.Message ?? "Rename failed.");
        return false; // keep edit mode open; caller re-focuses the editor
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Rename failed");
        return false;
    }
}

public void CancelRename(FileEntryViewModel entry)
{
    entry.IsEditing = false;
    entry.EditBuffer = string.Empty;
    if (ReferenceEquals(_activeEditingEntry, entry)) _activeEditingEntry = null;
}

// Moving the selection off the editing row cancels the rename cleanly.
partial void OnCurrentItemChanged(FileEntryViewModel? value)
{
    if (_activeEditingEntry is not null && !ReferenceEquals(_activeEditingEntry, value))
        CancelRename(_activeEditingEntry);
}
```

Notes:

- `CommitRenameAsync` takes a `candidateName` because `CellEditEnding` fires before the `TwoWay` binding has flushed the TextBox text into `EditBuffer`; the caller passes the `TextBox.Text` directly.
- The `bool` return lets the cell handler decide whether to cancel the TableView's own commit (returning `false` keeps edit mode open on invalid name / collision / handler failure).
- `RenameHandler.ExecuteAsync(FileSystemEntryModel, string, CancellationToken)` is unchanged. `MainShellViewModel.RenameAsync` is a thin call to `ActivePane.BeginRenameCurrent()`.

### 6.5. Edit lifecycle in the cell

`FileEntryTableView.xaml.cs` wires the TableView's edit lifecycle instead of raw TextBox events:

- `FileTable_BeginningEdit` — cancels the edit unless `e.Column == NameColumn` **and** `e.DataItem == host.ActiveEditingEntry`. This prevents accidental enter-edit from double-click on other columns.
- `FileTable_PreparingCellForEdit` — finds the descendant `TextBox`, dispatches `Focus(FocusState.Keyboard)` + `SelectNameStem(editor, entry.Name)` on the next tick (so the template has instantiated).
- `FileTable_CellEditEnding` — pushes the TextBox text into `entry.EditBuffer`; on `TableViewEditAction.Cancel` calls `host.CancelRename(entry)`; otherwise sets `e.Cancel = true` (suppress the control's own commit) and awaits `host.CommitRenameAsync(entry, entry.EditBuffer, CancellationToken.None)`. When the VM returns `false` (collision / invalid name), the editor is re-focused on the next dispatcher tick via `RefocusNameEditor(e.Cell)`.
- `FileTable_CellEditEnded` — currently a no-op seam kept for future instrumentation.
- `OnHostRenameRequested(_, entry)` — subscribed to `FilePaneViewModel.RenameRequested`; selects the row, sets `FileTable.CurrentCellSlot` to the Name column, scrolls it into view, and calls the TableView's internal `BeginCellEditing` / `EndCellEditing` via reflection (`BeginCellEditingMethod`, `EndCellEditingMethod` — load-bearing until `WinUI.TableView` ships public equivalents).
- `OnPreviewKeyDown` — when a row is already editing and the user presses `F2` or `Shift+F6` again, re-focus the existing editor instead of toggling edit off.

`MainShellView.xaml.cs` is the shell-level entry point for both keys:

```csharp
case VirtualKey.F2 when !ctrl && !shift && !inTextInputContext:
case VirtualKey.F6 when shift && !ctrl && !inTextInputContext:
    if (ViewModel.ActivePane.CurrentItem is { IsParentEntry: false })
    {
        ViewModel.RenameCommand.Execute(null);
        e.Handled = true;
    }
    break;
```

The `!inTextInputContext` gate prevents the keys from triggering while the path box, search box, or any other text surface has focus.

### 6.6. Focus and selection

When `RenameRequested` fires:

1. The view selects the row, sets `CurrentCellSlot` to the Name column, scrolls into view, and dispatches `BeginCellEditingMethod`.
2. `PreparingCellForEdit` runs on the next dispatcher tick, focuses the `TextBox` with `FocusState.Keyboard`, and selects the file **stem** (`Name.Length - Path.GetExtension(Name).Length`). If the stem length is `0` (dotfiles) the whole name is selected.
3. Clicking elsewhere triggers `CellEditEnding` with `TableViewEditAction.Commit`; the handler commits via the VM.
4. Pressing Escape triggers `CellEditEnding` with `TableViewEditAction.Cancel`; the handler routes to `CancelRename`.

### 6.7. Validation

- **Empty / whitespace** → the VM treats as cancel (no filesystem call) and returns `true` so the UI closes edit mode cleanly.
- **Unchanged** → same as empty: treated as cancel.
- **Invalid characters** (`Path.GetInvalidFileNameChars()`) → the VM returns `false` without invoking the handler; the cell handler cancels the TableView commit and re-focuses the editor. The v1 shipped surface keeps edit mode open as the only feedback.
- **Collision** (destination exists) → the handler returns a non-`Succeeded` summary; the VM returns `false`; same edit-stays-open behavior.
- **Successful rename** → the watcher emits `Renamed`; the pane's `SourceCache` replaces by `UniqueKey`. Focus returns to the row for the new entry.

> **Amended by `SPEC_RENAME_BUGS.md` R-2.** The silent "edit stays open" behavior proved too subtle in practice. The rename-bug spec adds a dismissible `InfoBar` surfaced through `FilePaneViewModel.RenameError` that reports the cause (collision / invalid chars / source gone / access denied / path too long). The editor stays open with `EditBuffer` intact; the banner can be dismissed with the `X` or via `Escape` focused on the banner. A red `VisualState` flash on the TextBox was considered and rejected in favor of the banner.

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

- [x] Grab the **left-right splitter**. Drag slowly 5 px at a time across 500 px. The left pane width tracks the pointer. No stutter on a 60 Hz display.
- [x] Grab the **inspector splitter**. Same test.
- [x] Drag each splitter at maximum pointer speed. The layout keeps up; no lag that exceeds one frame (~16 ms).
- [x] Open a 100 000-file folder (use `powershell/create-test-folder.ps1` / equivalent). Drag each splitter. Smoothness unchanged.
- [x] With the file list scrolled halfway down a 100 000-file folder, drag a splitter. The scroll position does not jump.

### 8.2. Column minimums

- [x] Drag the left-right splitter all the way left. The left pane stops at its `MinWidth = 320`.
- [x] Drag the inspector splitter all the way right. The inspector stops at its `MinWidth = 260`.
- [x] Both splitters at their minimums simultaneously: the right pane still has its `MinWidth = 320`. No column collapses.

### 8.3. Persistence

- [x] Resize each splitter to non-default widths. Resize two pane columns (e.g., Name → 400 px, Modified → 140 px). Change the sort on the left pane to "Size descending". Close the app (Alt+F4). Reopen. All widths, sort, and window placement restored.
- [ ] On a machine with the app's primary monitor disconnected (move settings file to a machine with a smaller screen), reopen. Window appears centered on the primary display, not off-screen.

### 8.4. In-place rename

- [x] Navigate to a test folder. Press `F2` on a file. The Name cell transitions to a TextBox; the **file stem** (not extension) is selected.
- [x] Type a new stem; press `Enter`. The file is renamed. Focus returns to the row; the renamed item is the sole selection and remains the current item.
- [x] Press `F2`, type a new name, press `Esc`. No rename. The original name is displayed.
- [x] Press `F2`, click elsewhere (outside the editor). Rename commits. Focus leaves edit mode.
- [x] Press `F2`, type a name containing `\`. No filesystem call occurs; the TextBox shows red / keeps edit mode.
- [x] Press `F2`, type a name that already exists in the folder. Error surfaces; edit mode stays open; user can fix.
- [x] Press `Shift+F6` — same behavior as `F2`.
- [x] Press `F2` on the `..` parent entry. Nothing happens.
- [x] Toolbar "Rename" button triggers the same in-cell edit.
- [ ] Rename a file inside a folder whose path is 400 chars (long path). In-cell rename works; capability policy does not disable it.

### 8.5. No modal rename remains

- [ ] `grep` the codebase for `ShowRenameDialogAsync` — zero results.
- [ ] `grep` for `"Rename"` in XAML `ContentDialog` instances — zero results in `Presentation/Services/WinUiDialogService.cs` (the method is deleted).

### 8.6. Inspector still renders correctly

- [x] Select a file; all inspector categories render. Category widths stretch to the inspector column width.
- [x] Resize the inspector; all expanders resize smoothly. No blank gaps, no clipping.
- [x] Select 0 files; the inspector empty state renders.
- [x] Select 2+ files; the inspector shows its multi-select state (currently: empty).

### 8.7. Regression — existing keyboard flows

- [x] Every shortcut in `winui-file-manager-keyboard-shortcuts-spec.md` §17 still behaves as documented.
- [x] Pane switch (`Tab`), path box focus (`Ctrl+L`), copy/move/delete shortcuts still work.

## 9. Acceptance

This spec is complete when:

- All items in §8 pass on a Windows 11 machine at 100% DPI and at 150% DPI.
- `MainShellView.xaml.cs` keeps `CommunityToolkit.WinUI.Controls.GridSplitter` as the sole resize surface. Splitter-adjacent pointer-start / pointer-end handlers are allowed only for the file-table width-freeze optimization documented in §3.3; they must not implement resize math.
- `UpdateInspectorLayout` is no longer called per pointer move.
- `FileInspectorViewModel.InspectorContentWidth` and `FileInspectorCategoryViewModel.ContentWidth` no longer exist.
- `IDialogService.ShowRenameDialogAsync` no longer exists; `WinUiDialogService` does not instantiate a rename `ContentDialog`.
- `Converters/` does **not** contain `BoolToVisibility` / `InverseBoolToVisibility` — the `TableViewTemplateColumn` `EditingTemplate` swap replaces them.
- `FilePaneViewModel` exposes `BeginRenameCurrent` / `CommitRenameAsync(entry, candidateName, ct)` (returns `bool`) / `CancelRename`, an `ActiveEditingEntry` observer, and a `RenameRequested` event. `OnCurrentItemChanged` cancels any in-flight rename when selection moves.
- `AppSettings` persists left-pane width, inspector width, per-pane column layout, per-pane sort, and main-window placement.
- First-launch (no settings file) uses the defaults in §3.1 and §5.
- Feature spec F11 is marked delivered.

### 9.1. Shipped status

| Batch | Scope | Status | Handoff note |
|---|---|---|---|
| U-1 | 5-column shell layout + `PixelGridLengthConverter` + `GridSplitter` ownership of resize behavior | shipped on `master` (no progress note) | — |
| U-2 | Inspector `ContentWidth` / `InspectorContentWidth` cascade removed; `SizeChanged` feedback loop gone | shipped | [ui-layout-batch-2.md](progress/ui-layout-batch-2.md) |
| U-3 | `AppSettings` persistence (pane widths, column layouts, sort, window placement); settings DTO made init-only | shipped | [ui-layout-batch-3.md](progress/ui-layout-batch-3.md) |
| U-4 | In-cell rename on the Name column; `ShowRenameDialogAsync` removed; F2 + Shift+F6 wired | shipped; manual acceptance complete | [ui-layout-batch-4.md](progress/ui-layout-batch-4.md) |
| U-5 | Status-bar XAML bindings cleanup per §4.3 (delete `UpdateStatusBar` / `OnPanePropertyChanged`; move composed strings to VM properties) | shipped | [ui-layout-batch-5.md](progress/ui-layout-batch-5.md) |

## 10. Non-goals

- Horizontal scrolling in either pane. The pane `MinWidth` prevents the list from becoming unusably narrow.
- Dockable / undockable inspector. Inspector remains a right-side column; visibility toggles with `Ctrl+I`.
- Drag-and-drop reordering of columns. Out of scope for v1.
- Multi-select rename (batch rename). Out of scope.
- Undo/redo of rename. Use the OS recycle bin model or see the broader feature roadmap.
