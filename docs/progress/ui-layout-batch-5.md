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
- Run the U-4 manual acceptance checklist from `docs/progress/ui-layout-batch-4.md` on a Windows 11 workstation at 100% and 150% DPI.
- Only after the manual U-4 pass is green, flip `ui-layout-batch-4.md` from `in progress` to `complete`.
- Continue with `SPEC_RENAME_BUGS.md` R-1 once U-4 manual acceptance is closed.

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
- U-4's manual wrap-up is still pending. This batch intentionally does not edit `ui-layout-batch-4.md` because the workstation-only checks were not run here.

## Context hints for the next agent
- The status bar is now pure binding flow: `MainShellView.xaml` -> `MainShellViewModel.ActivePaneLabel` / `ActivePane.*Display`.
- `FilePaneViewModel.NotifySelectionChanged()` and `NotifyItemCountChanged()` are the load-bearing notification seams for status-bar refreshes.
- If rename work needs a user-visible error surface next, start in `FilePaneViewModel` and `FilePaneView.xaml`; the status bar no longer participates in that flow.
