# Batch 2 of 4: Inspector Cascade Cleanup

**Spec:** `SPEC_UI_LAYOUT_AND_RESIZING.md` §2.2, §2.3, §4.1
**Branch merged into main:** `master`
**Status:** complete

## What shipped
- Removed the `InspectorContentWidth` observable property and the `UpdateInspectorContentWidth(double)` width-cascade helper from [FileInspectorViewModel.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/FileInspectorViewModel.cs:55).
- Removed the per-category `ContentWidth` observable property from [FileInspectorCategoryViewModel.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/FileInspectorCategoryViewModel.cs:1); categories now stretch through the ambient layout.
- Dropped the `Grid Width="{Binding InspectorContentWidth}"` wrapper around the category repeater and the `Width="{Binding ContentWidth}"` on each category `Expander` in [FileInspectorView.xaml](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Controls/FileInspectorView.xaml:69). The per-category Expanders stretch via `HorizontalAlignment="Stretch"` + `HorizontalContentAlignment="Stretch"`; the outer `ScrollViewer` keeps `HorizontalScrollBarVisibility="Disabled"` so the stretch width is unambiguous.
- Deleted the `SizeChanged` subscription, the `OnViewSizeChanged` handler, and the `GetInspectorContentWidth()` helper from [FileInspectorView.xaml.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Controls/FileInspectorView.xaml.cs:1); the view no longer pushes a width back into the VM per pointer move.

## What's next
- `SPEC_UI_LAYOUT_AND_RESIZING.md` batch `U-3` (persistence rollout): extend `AppSettings` with `LeftPaneWidth`, per-pane column layouts, per-pane sort state, and main-window placement; wire them through `PersistPaneStateCommandHandler` and `MainShellViewModel.InitializeAsync` / `PersistStateAsync`.
- Before starting U-3, verify the `PaneColumnLayout`, `SortState`, and `WindowPlacement` domain types do not yet exist (they are created in U-3) and the TableView column-resize-ended event wiring used by U-3.

## Acceptance results
- [x] `dotnet build WinUiFileManager.sln -c Release -p:Platform=x64` succeeds with 0 warnings and 0 errors (warnings-as-errors is enabled repo-wide).
- [x] `dotnet test` passes: 47 Application, 44 Infrastructure, 13 Interop (104 / 104 green) on Release|x64.
- [x] `FileInspectorViewModel.InspectorContentWidth` no longer exists (grep → zero hits in `src/`).
- [x] `FileInspectorCategoryViewModel.ContentWidth` no longer exists (grep → zero hits in `src/`).
- [x] `UpdateInspectorContentWidth`, `OnViewSizeChanged`, `GetInspectorContentWidth` no longer exist (grep → zero hits in `src/`).
- [skipped — manual only] §8.1 splitter smoothness checklist. The runtime environment for this batch is a sandbox without interactive GUI verification. The build runs cleanly and the app launches via `dotnet run -c Release -p:Platform=x64`; the human owner must complete the 60 Hz / 100 k-file drag checks on a workstation and record results in the next batch note if anomalies surface.

## Surprises
- Stale `obj/` and `bin/` directories left behind by prior runs (under `src/*/obj/x64/{Debug,Release,CodexDbg}/` plus `codex-artifacts/`, `artifacts/`, `.codex/`, `.codex-build/`) caused `CS0579` duplicate assembly-attribute errors on the first build attempt. They were removed before the successful clean build; this is a pre-existing environmental issue, not a code change.
- No tests referenced `InspectorContentWidth` / `ContentWidth` / `UpdateInspectorContentWidth`, so this batch has no test churn. `FileInspectorViewModelTests` exercises the batching and visibility paths without depending on the removed surface.

## Context hints for the next agent
- The 5-column shell layout and `PixelGridLengthConverter` binding for `LeftPaneColumn` / `InspectorColumn` are already in place (delivered in U-1): see [MainShellView.xaml](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Views/MainShellView.xaml:97). U-3 should only add the persistence-side plumbing.
- The Inspector ScrollViewer deliberately keeps `HorizontalScrollBarVisibility="Disabled"`; do not reintroduce a fixed-width wrapper Grid inside it or the stretch behavior will regress.
- Category ordering is still driven by `FileInspectorViewModel.GetCategorySortOrder`. The removed cascade was unrelated to ordering.
- The solution root has leftover top-level log files (`build-*.log`, `msbuild*.log`, `post-fix-build.log`, etc.); they are not part of this batch and were not touched.
