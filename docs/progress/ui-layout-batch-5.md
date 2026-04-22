# Batch 5 of 5: Status-Bar XAML Cleanup

**Spec:** `SPEC_UI_LAYOUT_AND_RESIZING.md` §4.3
**Branch merged into main:** `master` (local workspace, not committed in this session)
**Status:** complete

## What shipped
- `FilePaneViewModel` now owns the formatted status-bar strings through `PaneLabel`, `ItemCountDisplay`, and `SelectedDisplay`; the item/selection notifications raise the corresponding computed-property changes.
- `MainShellViewModel` now exposes `ActivePaneLabel` and assigns stable `PaneId` values to the left/right panes during construction.
- `MainShellView.xaml` binds the status-bar `TextBlock`s directly with `x:Bind`; `MainShellView.xaml.cs` no longer contains `UpdateStatusBar`, pane-level status subscriptions, or the byte-format helper.
- `ViewModelStatusBarDisplayTests.cs` covers the search suffix, selected-byte aggregate, and active-pane label.

## What's next
- Continue with `SPEC_RENAME_BUGS.md` R-1.
- Keep the UI-layout spec aligned with the shipped splitter-freeze optimization.
- Treat the UI-layout spec as closed unless new shell behavior is intentionally added.

## Acceptance results
- [x] `FilePaneViewModel` exposes `PaneLabel`, `ItemCountDisplay`, and `SelectedDisplay`.
- [x] `MainShellViewModel` exposes `ActivePaneLabel`.
- [x] `MainShellView.xaml` binds the status-bar row through `x:Bind` one-way.
- [x] `MainShellView.xaml.cs` no longer contains `UpdateStatusBar()`.
- [x] `MainShellView.xaml.cs` no longer subscribes to `LeftPane.PropertyChanged` / `RightPane.PropertyChanged` for status-bar refresh.
- [x] `FormatByteSize(long)` moved out of `MainShellView.xaml.cs`.
- [x] Three unit tests cover the new VM display properties.

## Surprises
- `MainShellViewModel` was the right place to assign `PaneId` values. That keeps `ActivePaneLabel` deterministic for first render without introducing another shell-level subscription.
- `StatusText` and its active-pane property-change plumbing were dead after the XAML move, so they were removed instead of being left as an unused parallel path.
- U-4 manual wrap-up completed after this note was first written. The spec/progress docs now record U-4 and U-5 as fully closed.

## Context hints for the next agent
- The status bar is now pure binding flow: `MainShellView.xaml` -> `MainShellViewModel.ActivePaneLabel` / `ActivePane.*Display`.
- `FilePaneViewModel.NotifySelectionChanged()` and `NotifyItemCountChanged()` are the load-bearing notification seams for status-bar refreshes.
- If rename work needs a user-visible error surface next, start in `FilePaneViewModel` and `FilePaneView.xaml`; the status bar no longer participates in that flow.
