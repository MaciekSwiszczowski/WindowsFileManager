# Batch 3 of 4: Persistence Rollout

**Spec:** `SPEC_UI_LAYOUT_AND_RESIZING.md` §5, §4.2, §4.3
**Branch merged into main:** `master`
**Status:** complete

## What shipped
### Domain (new value objects)
- Added [SortColumn.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Domain/Enums/SortColumn.cs:1) in `WinUiFileManager.Domain.Enums`. The enum previously lived under `Presentation.ViewModels`; it was promoted to Domain so `SortState` can depend on it without violating layering.
- Added [SortState.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Domain/ValueObjects/SortState.cs:1) (`readonly record struct`) with `Column` + `Ascending` and a `Default` of `(Name, Ascending)`.
- Added [PaneColumnLayout.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Domain/ValueObjects/PaneColumnLayout.cs:1) (`readonly record struct`) capturing the five column pixel widths (Name/Extension/Size/Modified/Attributes) plus a `Default` that matches the existing XAML widths.
- Added [WindowPlacement.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Domain/ValueObjects/WindowPlacement.cs:1) (`readonly record struct`) with `X/Y/Width/Height/IsMaximized`, a sentinel `Default` (`int.MinValue` position, 1400×900, not maximized), and a `HasRestoredPosition` helper.
- All three new structs carry `[StructLayout(LayoutKind.Auto)]` to satisfy the repo-wide `MA0008` analyzer (`readonly record struct` alone is not enough when multiple value-type fields are present; the existing `NtfsFileId` escapes the rule because its only field is a reference-type array).

### Application (settings + command handler)
- Extended [AppSettings.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Application/Settings/AppSettings.cs:1) with `LeftPaneWidth` (default `600`), `LeftPaneColumns`, `RightPaneColumns`, `LeftPaneSort`, `RightPaneSort`, and `MainWindowPlacement`. Nullable constructor params coalesce to the new `Default` statics so existing call sites (`new AppSettings()`) keep working.
- Extracted the command input into its own type [PersistPaneStateRequest.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Application/Settings/PersistPaneStateRequest.cs:1) (`readonly record struct`, `[StructLayout(LayoutKind.Auto)]`) with all persisted fields. This keeps the handler signature boring and avoids an 11-arg method.
- Rewrote [PersistPaneStateCommandHandler.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Application/Settings/PersistPaneStateCommandHandler.cs:1) around `ExecuteAsync(PersistPaneStateRequest, CancellationToken)`. The handler loads the current `AppSettings`, applies the request via a `with`-expression, and saves the result.

### Infrastructure (JSON persistence)
- Converted [SettingsDto.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Persistence/SettingsDto.cs:1) from a positional record to an init-only record so new optional fields are additive and tolerate older settings files (missing fields deserialize as `null`).
- Added sibling DTOs, one per file per project rules: [PaneColumnLayoutDto.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Persistence/PaneColumnLayoutDto.cs:1), [SortStateDto.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Persistence/SortStateDto.cs:1) (column stored as string), [WindowPlacementDto.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Persistence/WindowPlacementDto.cs:1).
- Extended [JsonSettingsRepository.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Persistence/JsonSettingsRepository.cs:84) `ToDomain` / `ToDto` mappers to cover the new fields. `ToDomain` defensively falls back to the corresponding `Default` statics for missing or non-positive values, so an old settings file restored after the upgrade never yields a zero-width column or a collapsed window.

### Presentation (view models + controls)
- [MainShellViewModel.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/MainShellViewModel.cs:1) now owns `LeftPaneWidth` and `MainWindowPlacement` (plus the existing `InspectorWidth`), applies persisted `ColumnLayout` and `SortState` into both `FilePaneViewModel`s during `InitializeAsync`, and sends a fully-populated `PersistPaneStateRequest` from `PersistStateAsync`.
- [FilePaneViewModel.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModel.cs:1) gained `ColumnLayout` (observable) and `SortState` (computed), plus `ApplySortState(SortState)` so the Main VM can restore sort without double-sorting the current view. `SortBy` / `SortAscending` now raise `PropertyChanged` for `SortState` via `[NotifyPropertyChangedFor]`.
- [FilePaneTableSortSync.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Controls/FilePaneTableSortSync.cs:1) was extended with `SyncColumnWidths(TableView, PaneColumnLayout)` and `CaptureColumnWidths(TableView, PaneColumnLayout)`. The setter uses `new GridLength(width, GridUnitType.Pixel)` because `TableViewColumn.Width` is a `GridLength` (not a `double`); the getter reads `ActualWidth` which is a `double`.
- [FileEntryTableView.xaml.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Controls/FileEntryTableView.xaml.cs:1): `Attach` now syncs the initial layout onto the TableView columns; `OnHostPropertyChanged` reacts to `ColumnLayout` changes (re-syncs), and a new `CaptureColumnLayoutIntoHost()` pushes the current widths back into the VM. Surface is exposed upward via [FilePaneView.xaml.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Panels/FilePaneView.xaml.cs:1) `CaptureColumnLayout()` and [MainShellView.xaml.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/Views/MainShellView.xaml.cs:1) `CapturePaneColumnLayouts()`.

### App (window placement)
- [MainShellWindow.xaml.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.App/Windows/MainShellWindow.xaml.cs:1) now:
  - Restores the persisted placement in `OnShellViewLoaded` (after `InitializeAsync`) via `ApplyPlacement`, clamping off-screen positions to the primary `DisplayArea` so a disconnected monitor cannot orphan the window.
  - On `OnAppWindowClosing`, calls `ShellView.CapturePaneColumnLayouts()`, writes the current `AppWindow.Position` / `AppWindow.Size` / maximized state into `_viewModel.MainWindowPlacement`, then awaits `PersistStateAsync()`.
  - Column widths are captured only on close (per the agreed design — no per-drag writes).

## What's next
- `SPEC_UI_LAYOUT_AND_RESIZING.md` batch `U-4` (in-cell rename): replace the modal rename dialog with in-place editing on the Name column of `TableView`. The persisted `PaneColumnLayout.NameWidth` from U-3 will already be in effect when rename starts; U-4 should not touch persistence.
- Before starting U-4, eyeball the TableView's `BeginEdit` / cell-template story — U-3 did not need it.

## Acceptance results
- [x] `dotnet build WinUiFileManager.sln -c Release -p:Platform=x64` succeeds with 0 warnings and 0 errors (warnings-as-errors is enabled repo-wide).
- [x] `dotnet test -c Release -p:Platform=x64` passes: 106 / 106 green (was 104; +2 in `JsonSettingsRepositoryTests`).
- [x] `AppSettings` round-trips all new fields via `JsonSettingsRepository` (`Test_SaveAndLoad_RoundTripsLayoutFields`).
- [x] An older settings file without the new fields loads without throwing and surfaces `PaneColumnLayout.Default`, `SortState.Default`, `WindowPlacement.Default` (`Test_LoadAsync_UsesDefaultsForMissingLayoutFields`).
- [skipped — manual only] §8.2 restart checklist (resize panes/columns, change sort, move/maximize window, quit, relaunch, verify restoration). The sandbox has no interactive GUI; the human owner should run this on a workstation and record results in the next batch note if anomalies surface.

## Surprises
- **Scope**: this batch touched 19 files across all four layers (Domain, Application, Infrastructure, Presentation, App). That exceeds the `SPEC_AGENT_BATCHING_PLAN.md` soft guideline (4-8 files / ≤ ~500 LOC). The user explicitly approved the single-batch rollout; splitting it would have required keeping `AppSettings` / DTO / handler signatures in two incompatible shapes across successive batches.
- **`SortColumn` relocation**: the existing enum was in `Presentation.ViewModels`. Because the new Domain type `SortState` has to reference it, the enum had to move to `Domain.Enums`. Three call sites (`FileEntryComparer`, `FileEntryTableViewModel`, `FileEntryComparerTests`) gained a `using WinUiFileManager.Domain.Enums;`.
- **`MA0008` on new structs**: `readonly record struct` with multiple value-type fields triggers `MA0008: Add StructLayoutAttribute`. Added `[StructLayout(LayoutKind.Auto)]` on `SortState`, `PaneColumnLayout`, `WindowPlacement`, and `PersistPaneStateRequest`. `NtfsFileId` doesn't trip this rule because its only field is a reference-typed `byte[]`.
- **`TableViewColumn.Width` is `GridLength`**: not `double`. `WinUI.TableView` exposes both `Width` (`GridLength`) for setting and `ActualWidth` (`double`) for reading; `FilePaneTableSortSync` uses both accordingly.
- **`CS8625` on the existing path conversion**: removing the `(NormalizedPath?)null` cast in `JsonSettingsRepository.ToDomain` made the compiler infer `NormalizedPath` (non-nullable struct) as the common branch type of the conditional and error out. The cast was restored.

## Context hints for the next agent
- `FilePaneViewModel.ColumnLayout` is the single source of truth for column widths. The View pushes current widths into it *only* on app close (via `MainShellWindow.OnAppWindowClosing → ShellView.CapturePaneColumnLayouts`). Do not wire a per-resize event handler in U-4.
- `FilePaneViewModel.ApplySortState` intentionally re-publishes the comparer on `_sortComparer`. Do not bypass it by setting `SortBy` / `SortAscending` directly when restoring persisted state — the comparer would lag one property change behind.
- `AppSettings` is an init-only record; prefer `settings with { … }` mutation and the `PersistPaneStateRequest` input. `PersistPaneStateCommandHandler` is the only write path.
- `WindowPlacement.Default` uses `int.MinValue` as the "unset" sentinel. `MainShellWindow.ApplyPlacement` must bail out when `!HasRestoredPosition` so the first launch keeps WinAppSDK's default centering.
- `SettingsDto` is now init-only and tolerates missing new fields. When adding more persisted fields in U-4, keep them nullable on the DTO and coalesce to a `Default` in `ToDomain`; that keeps backward compatibility for users who upgrade across a minor version.
- Top-level build log files (`build-*.log`, `msbuild*.log`, `post-fix-build.log`) left behind by prior batches were not touched.
