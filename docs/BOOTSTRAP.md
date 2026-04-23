# WinUI File Manager — Operational Bootstrap for Coding Agent

## Purpose

This document is the implementation bootstrap for the project.
It is stricter than the main product specification.
It defines the exact solution shape, project names, package choices, build defaults, folder tree, coding rules, and implementation order.

Use this document together with the main product spec.
If there is a conflict, this document wins for code generation details and repository layout.

## Primary Technical Decisions

- Target **Windows only**.
- Use **WinUI 3** on **Windows App SDK stable channel**.
- Use **.NET 10**.
- Use **C# latest** language version.
- Use **Microsoft.Windows.CsWin32** for Win32 interop generation.
- Use **CommunityToolkit.Mvvm** for MVVM support.
- Use **TUnit** for tests.
- Use **one file per type**.
- Use **layered architecture** with strict dependency direction.
- Support **NTFS only**.
- Use **unpackaged app** for v1 unless a packaging requirement is later added.

## Current Baseline to Use

At the time of generating this document:

- **.NET 10** is the current LTS baseline.
- **Windows App SDK 1.8 stable** is the production-ready stable channel baseline.
- **Microsoft.Windows.CsWin32** is the current replacement for the older PInvoke package family and should be used for generated Win32 bindings.

Do not downgrade these choices unless a concrete blocker is proven.

## Build and Sandbox Notes

- Prefer serial builds when working in the Codex sandbox: use `-m:1` for `dotnet build` when you are verifying a repo-wide change.
- Do not run multiple build commands against the same workspace in parallel. Shared `obj` and `bin` outputs can race on generated cache files.
- If Rider or another IDE is open, use an isolated sandbox build tree instead of the default project `obj` and `bin` folders: pass `-p:UseCodexIsolatedBuild=true`. This redirects intermediates to `codex-artifacts\obj\<ProjectName>\` and outputs to `codex-artifacts\bin\<ProjectName>\`, which avoids lock contention with the IDE without changing normal developer builds.
- Restore runs offline-safe: `NuGetAudit` is disabled at the repo level because sandbox builds do not have network access to the NuGet vulnerability feed.
- If a test project fails on `.msCoverageExtensionSourceRootsMapping_*` during a plain `build`, treat that as a build-environment issue and suppress build-time coverage path-map generation at the repo level instead of changing individual commands.
- If the WinUI XAML compiler only reports `XamlCompiler.exe exited with code 1`, inspect the generated `obj\...\input.json` and `output.json` under `src/WinUiFileManager.Presentation` before assuming the UI code is broken.
- When build failures are opaque in the sandbox, verify the smallest project first, then the full solution.

## Solution and Project Names

Create a solution named:

- `WinUiFileManager.sln`

Create these projects exactly:

- `src/WinUiFileManager.App`
- `src/WinUiFileManager.Presentation`
- `src/WinUiFileManager.Application`
- `src/WinUiFileManager.Domain`
- `src/WinUiFileManager.Infrastructure`
- `src/WinUiFileManager.Interop`
- `tests/WinUiFileManager.Application.Tests`
- `tests/WinUiFileManager.Infrastructure.Tests`
- `tests/WinUiFileManager.Interop.Tests`

### Project Responsibilities

#### `WinUiFileManager.App`
Host project.
Contains app startup, DI composition root, WinUI bootstrap, shell window, app-level resources, and navigation bootstrap.
No business logic.

**Startup wiring**: `MainShellWindow.OnActivated` resolves `MainShellViewModel` from DI, sets `PaneId` on both panes, injects `XamlRoot` into `WinUiDialogService`, calls `ShellView.Initialize(viewModel)`, then `viewModel.InitializeAsync()` which loads NTFS drives and navigates both panes to the first available drive root.

#### `WinUiFileManager.Presentation`
ViewModels, UI state models, XAML views, keyboard command bindings, converters, dialog abstractions.
No filesystem logic.
No direct Win32 interop.

#### `WinUiFileManager.Application`
Use cases, command handlers, operation planning, policies, DTOs, interfaces for services.
This is the main orchestration layer.
No direct WinUI types.
No direct Win32 calls.

#### `WinUiFileManager.Domain`
Pure domain types and rules.
Contains value objects, enums, operation results, operation plans, path identity abstractions, and invariants.
No infrastructure dependencies.

#### `WinUiFileManager.Infrastructure`
Concrete filesystem engine, persistence for favourites and settings, logging implementation, path normalization, operation execution services.
Can depend on `Interop`.
Must not depend on Presentation.

#### `WinUiFileManager.Interop`
Generated Win32 bindings and thin adapter wrappers around generated APIs.
No business decisions.
No WinUI code.

#### Test Projects
- `Application.Tests`: command layer integration tests
- `Infrastructure.Tests`: filesystem engine tests on real NTFS temp directories
- `Interop.Tests`: targeted tests for interop wrappers and NTFS-specific behavior

## Dependency Direction

Use only this direction:

- `App` -> `Presentation`
- `App` -> `Application`
- `App` -> `Infrastructure`
- `Presentation` -> `Application`
- `Application` -> `Domain`
- `Infrastructure` -> `Application`
- `Infrastructure` -> `Domain`
- `Infrastructure` -> `Interop`
- `Interop` -> none

Forbidden:

- `Presentation` -> `Infrastructure`
- `Presentation` -> `Interop`
- `Domain` -> anything
- `Application` -> `Presentation`
- `Application` -> `Interop`

## Required Repository Layout

Use this exact top-level layout:

```text
/WinUiFileManager.sln
/Directory.Build.props
/Directory.Build.targets
/Directory.Packages.props
/global.json
/README.md
/docs/
  SPEC_V1.md
  AGENT_BRIEF.md
  BOOTSTRAP.md
/src/
  WinUiFileManager.App/
  WinUiFileManager.Presentation/
  WinUiFileManager.Application/
  WinUiFileManager.Domain/
  WinUiFileManager.Infrastructure/
  WinUiFileManager.Interop/
/tests/
  WinUiFileManager.Application.Tests/
  WinUiFileManager.Infrastructure.Tests/
  WinUiFileManager.Interop.Tests/
```

## Required Internal Folder Structure per Project

### `src/WinUiFileManager.App`

```text
App.xaml
App.xaml.cs
Composition/
Windows/
Resources/
Themes/
```

### `src/WinUiFileManager.Presentation`

```text
Commands/
Converters/
Dialogs/
Models/
Panels/
Services/
ViewModels/
Views/
```

### `src/WinUiFileManager.Application`

```text
Abstractions/
Commands/
Common/
Favourites/
FileOperations/
Navigation/
Properties/
Selection/
Settings/
```

### `src/WinUiFileManager.Domain`

```text
Enums/
Errors/
Events/
Operations/
Results/
ValueObjects/
```

### `src/WinUiFileManager.Infrastructure`

```text
Execution/
FileSystem/
Logging/
Persistence/
Planning/
Services/
Utilities/
```

### `src/WinUiFileManager.Interop`

```text
Generated/
Adapters/
Constants/
Types/
```

### `tests/*`

```text
Fixtures/
Helpers/
Scenarios/
```

## Package Management Rules

Use **Central Package Management**.
All package versions must be declared in `Directory.Packages.props`.
Project files must reference packages without inline versions unless there is a justified exception.

## Required NuGet Packages

### Solution-Level Required Packages

Define these package IDs centrally:

- `Microsoft.WindowsAppSDK`
- `CommunityToolkit.Mvvm`
- `CommunityToolkit.WinUI.Controls.Sizers`
- `DynamicData`
- `Microsoft.Windows.CsWin32`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`
- `System.Text.Json`
- `TUnit`

### Package Assignment by Project

#### `WinUiFileManager.App`
- `Microsoft.WindowsAppSDK`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Options`

#### `WinUiFileManager.Presentation`
- `CommunityToolkit.Mvvm`
- `CommunityToolkit.WinUI.Controls.Sizers` (`GridSplitter` for column resize in the pane header)
- `DynamicData`
- `Microsoft.Extensions.Logging.Abstractions`

#### `WinUiFileManager.Application`
- `Microsoft.Extensions.Logging.Abstractions`

#### `WinUiFileManager.Infrastructure`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Options`
- `System.Text.Json`

#### `WinUiFileManager.Interop`
- `Microsoft.Windows.CsWin32`

Configure CsWin32 with `PrivateAssets="all"` and include build assets only as needed.
Do not expose it as a transitive runtime dependency.

#### Test Projects
- `TUnit`
- Any additional Microsoft Testing Platform support package only if required by the chosen template or SDK tooling

## Package Version Policy

Use the latest stable versions available at implementation time.
Do not use preview packages unless a stable package is missing a required feature.
If a preview package becomes necessary, document the reason in `/docs/BOOTSTRAP.md` and isolate the impact.

## `global.json`

Create a `global.json` pinned to the latest installed .NET 10 SDK used during implementation.
Use roll-forward only within the same feature band when possible.

Example shape:

```json
{
  "sdk": {
    "version": "10.0.xxx",
    "rollForward": "latestFeature"
  }
}
```

Replace `10.0.xxx` with the actual SDK version available on the development machine.

## `Directory.Build.props`

Create a strict shared build configuration.
Use these defaults unless there is a justified project-specific override:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <DebugType>portable</DebugType>
  </PropertyGroup>
</Project>
```

## `Directory.Build.targets`

Add guard rails:

- Fail the build if a project references another project in the wrong architectural direction.
- Fail the build if a package version is declared inline instead of centrally.
- Fail the build if forbidden folders are introduced for generated code outside the approved places.

## Project File Defaults

### `WinUiFileManager.App.csproj`

Requirements:

- WinUI desktop app
- unpackaged app for v1
- output type `WinExe`
- Windows App SDK enabled

Shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <RootNamespace>WinUiFileManager.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WinUiFileManager.Presentation\WinUiFileManager.Presentation.csproj" />
    <ProjectReference Include="..\WinUiFileManager.Application\WinUiFileManager.Application.csproj" />
    <ProjectReference Include="..\WinUiFileManager.Infrastructure\WinUiFileManager.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Microsoft.Extensions.Options" />
  </ItemGroup>
</Project>
```

### `WinUiFileManager.Interop.csproj`

Requirements:

- CsWin32 only here
- generated files go to `Generated/`
- no hand-written DllImport unless unavoidable

Shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>WinUiFileManager.Interop</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.CsWin32" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="NativeMethods.txt" />
  </ItemGroup>
</Project>
```

## CsWin32 Rules

Use **Microsoft.Windows.CsWin32**.
This is mandatory.
Do not create manual P/Invoke declarations for APIs that CsWin32 can generate.

### Required `NativeMethods.txt`

Start with at least these APIs and add more only when required:

```text
CreateDirectoryW
CreateFileW
CopyFile2
DeleteFileW
FindClose
FindFirstFileExW
FindNextFileW
GetFileAttributesExW
GetFileInformationByHandleEx
GetLogicalDrives
GetVolumeInformationW
MoveFileExW
RemoveDirectoryW
SetFileAttributesW
WIN32_FIND_DATAW
FILE_ID_INFO
FILE_ID_128
COPYFILE2_EXTENDED_PARAMETERS
```

### Interop Rules

- Wrap generated APIs behind small adapter interfaces when the rest of the application needs them.
- Keep generated types in `Generated/`.
- Keep hand-written wrappers in `Adapters/`.
- Never let Presentation call generated P/Invoke directly.
- Never mix business logic with interop code.

### IFileOperationInterop

The `IFileOperationInterop` adapter provides low-level file system operations:

- `CopyFile(source, destination, overwrite)` — `File.Copy`
- `MoveFile(source, destination, overwrite)` — `File.Move` (files only)
- `MoveDirectory(source, destination)` — `Directory.Move` (atomic rename on same volume)
- `DeleteFile(path)` — `File.Delete`
- `RemoveDirectory(path)` — `Directory.Delete(path, recursive: false)`
- `CreateDirectory(path)` — `Directory.CreateDirectory`
- `SetFileAttributes(path, attributes)` — `File.SetAttributes`

## File-Per-Type Rule

This is mandatory.

### Rules

- One top-level type per file.
- File name must match the type name.
- No multiple classes in one file.
- No multiple records in one file.
- No nested types unless there is a strong technical reason.
- Enums also get their own files.
- Exceptions also get their own files.
- Interfaces also get their own files.

### Allowed Exceptions

- Small private helper types local to generated code only.
- Partial class splits when required by WinUI/XAML or source generators.

## Modern C# Style Rules

Use modern, conservative C#.
Prefer clarity over novelty.

### Required

- File-scoped namespaces.
- `sealed` by default for concrete types unless inheritance is intended.
- Constructor injection.
- `async`/`await` all the way.
- `CancellationToken` on async operations that may block or do IO.
- `record` or `readonly record struct` for immutable DTO/value-style models where appropriate.
- `required` members when appropriate.
- Pattern matching where it improves clarity.
- `var` when the type is obvious from the right-hand side.
- Expression-bodied members only when they remain readable.
- `nameof(...)` instead of string literals for member names.

### Avoid

- Service locators.
- Static mutable global state.
- `async void` except for true event handlers.
- giant God classes.
- code-behind business logic.
- `#region` blocks.
- comments that restate the code.
- hand-written Win32 signatures that CsWin32 can generate.

## WinUI Rules

### UI Principles

- Keyboard-first operation is mandatory.
- Exactly one active pane at a time.
- All primary commands must be reachable from the keyboard.
- Avoid mouse-only interactions.
- Focus transitions must be explicit and deterministic.

### Preferred Structure

- Main shell window
- left panel
- right panel
- command bar or key hint area
- status bar
- modal dialogs only where necessary

### UI Technology Rules

- Use MVVM.
- Keep logic out of XAML code-behind except for view-only plumbing.
- Use `KeyboardAccelerator` for shortcuts.
- Use virtualization-friendly list controls.
- Do not optimize for visual flourish before keyboard correctness and correctness of filesystem operations.

### Theme System

- Default theme is Dark, set on the content root `FrameworkElement.RequestedTheme` in the Window constructor.
- Do **not** set `RequestedTheme` in `App.xaml` — it blocks dynamic theme switching.
- The root `Grid` of `MainShellView` must have `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"` so the background adapts to theme changes.
- Title bar colors are synchronized manually via `AppWindow.TitleBar` properties.
- Toggle theme toggles `root.RequestedTheme` between `ElementTheme.Dark` and `ElementTheme.Light`.

### Startup and Lazy Loading

- The shell window renders immediately with empty panes showing a loading overlay.
- `OnShellViewLoaded` calls `Task.Yield()` before `InitializeAsync()` to let the UI paint first.
- Drive loading and directory enumeration happen asynchronously after the UI is visible.
- Each pane shows a top-aligned indeterminate `ProgressBar` overlay while a directory scan is in flight.
- On folder activation (`Enter` or double-click), the pane updates `CurrentPath` immediately, inserts the synthetic `..` row first, and makes that row the current item before enumeration finishes.
- The pane keeps the progress bar visible until the full directory scan completes.
- If the requested directory disappears during the load, the pane falls back to the nearest existing ancestor instead of staying stranded on a dead path.

### Large Directory Performance (DynamicData)

- `FilePaneViewModel` uses a `SourceCache<FileEntryViewModel, string>` (from the DynamicData NuGet) as the authoritative data store.
- A `BehaviorSubject<IComparer<FileEntryViewModel>>` drives reactive sorting; `.SortAndBind(out _sortedItems, comparer)` produces a `ReadOnlyObservableCollection<FileEntryViewModel>` that is assigned to `ListView.ItemsSource`.
- On navigation, `SourceCache.Edit(updater => { updater.Clear(); ... })` replaces all items in a single batch, emitting one reset notification.
- `SourceCache.Remove(keys)` and `SourceCache.AddOrUpdate(items)` provide surgical delta updates after file operations, preserving selection state.
- Re-sorting happens reactively by pushing a new `FileEntryComparer` into the `BehaviorSubject`.
- `ListView` handles UI virtualization natively — only visible items are rendered regardless of total count.

### Inspector Reactive Loading

- The inspector selection pipeline lives in `MainShellViewModel`, not in `FileInspectorViewModel`.
- Do not use `Subject` for inspector selection state.
- Convert pane selection changes into an `IObservable<Unit>` or equivalent signal stream, then derive two separate paths from it.
- The basic inspector path updates immediately on the UI thread and may only touch already-available selected-entry data.
- The deferred inspector path must switch to the background scheduler before throttling, then apply `Throttle(TimeSpan.FromMilliseconds(200), ...)` so rapid keyboard navigation collapses into one load once the user pauses.
- Inspector refresh must reuse the same reactive pipeline. A manual Refresh action should emit a refresh signal into the observable stream and force the current single selection to be re-read, even when the selected row did not change.
- Deferred inspector invalidation must be based on the actual selection identity, not on unrelated pane loading churn. Do not bump the deferred selection token just because `IsLoading` changed.
- Load deferred inspector batches on the background scheduler. Keep each batch category self-contained so future property groups can be added without changing the selection pipeline.
- Deferred categories such as `IDs`, `Locks`, `Links`, `Streams`, `Security`, and `Thumbnails` must be loaded independently as separate batches. `NTFS File/Folder ID` belongs to the `IDs` batch and must not be folded into the immediate/basic selection path.
- The `Thumbnails` batch can surface both a preview and lightweight association metadata. Keep the preview optional and hide it entirely when no thumbnail bytes are available.
- The `NTFS` category is immediate and belongs with the cheap basic state. It should surface managed file attributes as separate Yes/No rows such as `Read Only`, `Hidden`, `System`, `Archive`, `Temporary`, `Offline`, `Not Content Indexed`, `Encrypted`, `Compressed`, `Sparse`, and `Reparse Point`.
- The `NTFS` category must also support a deferred live-metadata batch. Use it to refresh NTFS attribute flags from a native handle and to surface the four NTFS timestamps: creation, last access, last write, and MFT change time.
- `MFT Changed` comes from `FILE_BASIC_INFO.ChangeTime` read through `GetFileInformationByHandleEx`. If that value is always missing, treat the native metadata call path as broken before changing the UI.
- Deferred batches must be applied incrementally as they complete. Do not wait for all deferred categories to finish before publishing the first completed batch to the UI.
- Return to the UI thread only after the deferred batch results are ready, and only to apply bound view-model state.
- Do not read filesystem or WinRT-backed data from the UI thread.
- Do not update `ObservableCollection` or visible inspector field values from background threads.
- The grouped inspector UI must keep category view models alive across selection changes. Do not rebuild the category collection on every update; only update field values and field visibility within existing category objects.
- The grouped inspector row model must expose an explicit `IsVisible` flag per field. Refresh walks the stable field objects, updates values in place, and recomputes `IsVisible`. Do not create or destroy inspector rows during refresh.
- Category membership must also be stable. Create each category's field list once during inspector initialization, keep those row references alive, and let the view hide rows by `IsVisible` instead of rebuilding per-category collections.
- A refresh of the same selected item must not collapse existing deferred categories or hide already-visible deferred rows while new data is loading. Keep those rows visible, mark them as loading, and defer the final hide/show decision until the last deferred batch completes.
- During refresh, currently visible deferred rows may render a small spinner or `Loading...` in place of the value. Do not blank the value cell and do not collapse the row during the load, because that causes expander churn and scroll jitter.
- Grouped inspector categories use persistent `Expander` sections, but the inner property list should be rendered with a simple two-column `Grid` layout, not nested `TableView` controls. Use a fixed `Property` column and a star-sized `Value` column so the value side always takes the remaining inspector width.
- When the grouped inspector is inside a `ScrollViewer`, do not rely on nested `ItemsControl` presenters to stretch category content. Host the grouped content inside a finite-width parent and prefer `ItemsRepeater` + `StackLayout` so the expander host, not item presenters, controls width.
- In practice, grouped category width should be driven from measured view size, not only from template stretch. The view may publish the current available content width into the inspector/category view models, and grouped `Expander` sections may bind their width to that measured value.
- Use `DynamicData` for large live lists where it already exists in the pane stack; use Rx for event orchestration and throttling, not for manually pushing collection mutations from arbitrary threads.
- Prefer `static` lambdas where no instance capture is needed.
- Do not replace lambdas with helper methods just to satisfy this rule. The rule is about marking existing non-capturing lambdas as `static`, not refactoring lambda-based Rx or DynamicData pipelines into method groups or helper methods unless there is a separate readability reason.

### Inspector Field Visibility And Tooltips

- Inspector tooltips must explain the user-facing meaning of the property. Do not expose implementation details such as internal Win32, COM, Shell, or interface names in tooltips.
- Inspector rows are dynamic. A field should be shown only when its value is non-empty.
- The `Locks` category must contain a stable summary property named `Is locked`. Once lock diagnostics have loaded, this property stays visible in the category and shows `True` or `False` based on the other lock diagnostics in that category.
- `Is locked` must be `True` only when there is positive lock evidence in the category, such as `In Use = Yes`, a non-empty locker list, non-empty lock PIDs or services, non-empty usage text, or affirmative capability flags. Missing data or all-empty diagnostics must not be treated as locked.
- Do not show temporary `Loading...` rows for deferred categories.
- Do not show category-specific loading messages while deferred categories are being fetched. Keep deferred rows hidden until real values arrive.
- When a file or folder is locked, show `Is locked = True` and only the additional lock fields that have non-empty values.
- When a file or folder is not locked, show `Is locked = False` and hide the other empty lock fields.
- Keep lock-diagnostic labels and tooltips easy to understand:
  - `Locked By` identifies the applications or services using the item.
  - `Lock PIDs` helps correlate the lock with Task Manager or Process Explorer.
  - `Lock Services` helps identify background-service locks.
  - `Usage`, `Can Switch To`, and `Can Close` are advanced diagnostics and must be described in plain language.
- Keep identity / link / stream / security / thumbnail labels equally plain-language:
  - `File ID`, `Volume Serial`, `File Index (64-bit)`, `Hard Link Count`, and `Final Path` belong in `IDs`.
  - `Link Target`, `Link Status`, `Reparse Tag`, `Reparse Data`, and `Object ID` belong in `Links`.
  - `Alternate Stream Count` and `Alternate Streams` belong in `Streams`.
  - `Owner`, `Group`, `DACL Summary`, `SACL Summary`, `Inherited`, and `Protected` belong in `Security`.
  - `Has Thumbnail` and `Association` belong in `Thumbnails`, while any preview UI stays optional.
- `Cloud` is the last deferred inspector category. Keep it hidden unless the selected item is cloud-controlled by sync-root registration, provider identity, or placeholder state.
- The cloud summary should prefer plain labels such as `Hydrated`, `Dehydrated`, `Pinned`, `Synced`, and transfer labels like `Upload pending` or `Transferring`. Append provider-defined custom text only when Windows exposes it.
- Only a narrow subset of NTFS yes/no rows should expose inline toggles: `Read Only`, `Hidden`, `Archive`, `Temporary`, and `Not Content Indexed`. Leave derived, privileged, and provider-managed flags read-only.
- The inspector header must include a manual Refresh action so the user can re-read diagnostics after external changes, such as closing or killing the locking process.
- Unsupported inspector selections must clear the inspector completely:
  - multiselection does not show inspector content
  - the synthetic parent row `..` does not show inspector content
  - selection-signature code must never dereference `entry.Model` for the synthetic parent row

### Column Sorting

- Clickable column header buttons in `FilePaneView` invoke `FilePaneViewModel.SetSort(SortColumn)`.
- Clicking the active sort column toggles ascending/descending; clicking a different column switches to ascending.
- `FileEntryComparer` always sorts `..` (parent entry) first, then directories before files, then by the selected column.
- Sort indicators (▲ / ▼) appear on the active sort column header.

### Column Resizing

- Use **`CommunityToolkit.WinUI.Controls.GridSplitter`** between header columns (Windows Community Toolkit for WinUI 3). It resizes the parent `Grid` column definitions correctly; do not hand-roll pointer splitters.
- The `ListView` item template uses the **same column count and order** as the header (`*` Name column, splitter columns, fixed data columns). **Same root `Padding="8,4"`** on header grid and item row — no extra `ColumnSpacing` on the item row, or headers and values misalign.
- After a splitter finishes a drag (`PointerReleased` / `PointerCaptureLost` / `ManipulationCompleted`), copy each header `ColumnDefinition.Width` and `MinWidth` onto the corresponding column in each **realized** `ListViewItem` content root (`ContainerContentChanging` tracks rows).
- Toggle resizing globally with **`FilePaneDisplayOptions.EnableColumnResize`** (`true` = splitters visible; `false` = splitter columns collapsed to zero width and controls hidden). Apply on pane `Loaded` (restart or re-open pane after changing the flag unless you add your own refresh hook).

### Selection Model

- `TableView.SelectionMode="Extended"` enables native single-click, Ctrl+click, and Shift+click multi-selection.
- Selection state is owned by the control. Do not duplicate it with per-row `IsSelected` flags on `FileEntryViewModel`.
- Space/Insert toggle selection through `TableView.SelectedItems`, and command targeting is derived from the pane's current control selection.
- On folder activation (`Enter` or double-click), the pane navigates immediately and the synthetic `..` row becomes the current item for non-root directories while the new folder is still loading.
- `PageUp`, `PageDown`, `Home`, and `End` must work in the pane grid even if the third-party table control does not implement them correctly. Handle them explicitly in preview key routing so paging/navigation does not depend on undocumented control behavior.
- The inspector refresh signal must include pane-load completion (`IsLoading` transitioning to `false`). Otherwise the inspector can get stuck after a pane clears itself during loading and never repopulates when loading finishes.
- After file operations (copy, move, delete, rename, create folder), `FocusActivePaneRequested` re-focuses the active pane's file list.

### Command Bar

- `CommandBar` with `DefaultLabelPosition="Collapsed"`, `HorizontalAlignment="Left"`, `OverflowButtonVisibility="Collapsed"` ensures all buttons are visible left-aligned as icon-only buttons with tooltips.
- Toggle Theme is a primary command button, not hidden in secondary commands.

### Selection Visibility

- Custom `ListViewItemBackgroundSelected` theme resources are defined in `App.xaml` `ThemeDictionaries` for both Dark and Light themes.
- Dark theme: blue-tinted selection highlight (`#3380A0FF`).
- Light theme: blue-tinted selection highlight (`#440060D0`).

## Domain Model Requirements

Create at least these domain concepts, each in its own file:

- `PaneId`
- `ItemKind`
- `FileSystemEntryModel`
- `FavouriteFolder`
- `OperationType`
- `OperationPolicy`
- `OperationPlan`
- `OperationItemPlan`
- `OperationStatus`
- `OperationSummary`
- `CollisionPolicy`
- `ParallelExecutionOptions`
- `NtfsFileId`
- `NormalizedPath`
- `OperationError`
- `OperationWarning`

## Required Application Interfaces

Create these interfaces in `Application/Abstractions`:

- `IFileSystemService`
- `IFileOperationService`
- `IFileOperationPlanner`
- `IPathNormalizationService`
- `INtfsVolumePolicyService`
- `IFileIdentityService`
- `IFavouritesRepository`
- `ISettingsRepository`
- `IClipboardService` → `WinUiClipboardService` (Singleton in DI)
- `IDialogService` → `WinUiDialogService` (Singleton in DI, requires `XamlRoot` injection after window activation)
- `ITimeProvider`

## Required Commands

Create one handler type per command.
Do not combine unrelated commands into a single class.

### Navigation
- `OpenEntryCommandHandler`
- `NavigateUpCommandHandler` — also triggered by clicking the synthetic `..` parent entry at the top of each directory listing (except at drive roots)
- `GoToPathCommandHandler`
- `RefreshPaneCommandHandler`
- `SwitchActivePaneCommandHandler`

### Selection convention
When no items are explicitly selected (via Space/Insert), commands operate on the current (focused) item. The `..` parent entry is excluded from command targets.

### Operations
- `CopySelectionCommandHandler` — plans via `IFileOperationPlanner.PlanCopyAsync`, executes via `IFileOperationService`
- `MoveSelectionCommandHandler` — plans via `IFileOperationPlanner.PlanMoveAsync`, executes via `IFileOperationService`. After moving all files, source directories are cleaned up automatically.
- `RenameEntryCommandHandler` — builds a single-item `OperationPlan` with `OperationType.Rename` directly (no planner). Directory renames use `Directory.Move` for atomicity; file renames use `File.Move`. Only depends on `IFileOperationService` (no planner or path normalization).
- `BatchRenameSelectionCommandHandler`
- `DeleteSelectionCommandHandler` — plans via `IFileOperationPlanner.PlanDeleteAsync`, executes via `IFileOperationService`
- `CreateFolderCommandHandler` — plans via `IFileOperationPlanner.PlanCreateFolderAsync`
- `ShowPropertiesCommandHandler`
- `CopyFullPathCommandHandler`

### Favourites
- `AddFavouriteCommandHandler`
- `RemoveFavouriteCommandHandler`
- `OpenFavouriteCommandHandler`

### Settings
- `SetParallelExecutionCommandHandler`

## Required Infrastructure Services

Create these concrete types in `Infrastructure`, one per file:

- `WindowsFileSystemService`
- `WindowsFileOperationService` — executes `OperationPlan` items sequentially or in parallel. For `Copy`/`Move` directory items, creates the destination directory. For `Move` operations, automatically cleans up empty source directories after all items complete. For `Rename` operations, uses `MoveFile` (files) or `MoveDirectory` (directories) for atomic renames.
- `WindowsFileOperationPlanner`
- `WindowsPathNormalizationService`
- `NtfsVolumePolicyService`
- `NtfsFileIdentityService`
- `JsonFavouritesRepository`
- `JsonSettingsRepository`
- `StructuredOperationLogger`

## Settings Rules

Persist settings as JSON.
Use a stable user-local application data location.

Required settings:

- active parallelization enabled/disabled
- max degree of parallelism
- favourite folders
- last used pane paths
- optional window state

Do not store volatile UI selection state unless required later.

## NTFS Policy Rules

- Ignore non-NTFS volumes completely.
- They must not appear in normal drive lists.
- They must not be accepted as navigation targets.
- They must not be accepted as operation source or destination.
- If a path is explicitly pasted and is not NTFS, reject it with a clear validation error.

## File Identity Rules

- Display **FileId only** in the UI.
- Do not display volume serial number in the UI.
- Internal services may keep richer identity context if required.
- FileId retrieval must use `GetFileInformationByHandleEx(..., FileIdInfo, ...)` via CsWin32-generated bindings.
- Treat FileId display as diagnostic metadata, not a user-facing permanent business identifier.

## Long Path Rules

- The app must be long-path aware.
- Normalize paths for engine operations.
- Preserve user-visible path text separately from engine-normalized path form when useful.
- Shell UI integrations such as the Windows Properties dialog must receive the user-visible display path, not the internal normalized `\\?\` form.
- Test long paths explicitly.

## Error Handling Rules

- No swallowed exceptions.
- Convert Win32 failures to domain/application-level results.
- Preserve native error code information in logs and operation summaries.
- Locked files, access denied, collisions, and cancelled operations must be reported as structured results.
- Batch operations must return a complete summary even when partially failing.

## Logging Rules

Use structured logging.

Log at minimum:

- operation start
- operation finish
- per-item failure
- cancellation
- retry decision
- collision resolution decision
- rejected non-NTFS path

## TUnit Test Rules

### General

- Use TUnit for all test projects.
- Focus on command-layer and engine/infrastructure integration tests.
- Keep UI automation light.
- Run tests on real NTFS temp directories.

### Required Fixture Types

Create one file per fixture:

- `NtfsTempRootFixture`
- `LargeDirectoryFixture`
- `CollisionFixture`
- `LockedFileFixture`
- `LongPathFixture`
- `ReparsePointFixture`
- `SettingsFixture`

### Minimum Required Command Coverage

Each implemented command must have integration tests.

At minimum, cover:

- successful execution
- validation failure
- cancellation where applicable
- partial failure where applicable
- locked file behavior where applicable
- long-path behavior where applicable
- non-NTFS rejection where applicable

### Test Naming

Use descriptive names such as:

- `CopySelectionCommandHandler_Copies_Multiple_Selected_Files`
- `DeleteSelectionCommandHandler_Returns_Warnings_For_Locked_Files`
- `GoToPathCommandHandler_Rejects_NonNtfs_Path`

## Initial Implementation Order

Follow this order.
Do not jump ahead.

### Phase 1
- solution structure
- central package management
- shared build props/targets
- WinUI app bootstrap
- DI composition root

### Phase 2
- domain types
- application abstractions
- settings and favourites repositories
- NTFS volume policy service
- path normalization service

### Phase 3
- CsWin32 integration
- file identity service
- filesystem enumeration service
- properties retrieval service

### Phase 4
- dual-pane shell UI
- pane ViewModels
- keyboard navigation
- refresh and go-to-path
- favourites UI

### Phase 5
- copy/move/delete/create folder
- collision policy
- cancellation
- structured operation summaries

### Phase 6
- batch rename
- parallel execution switch
- test expansion and hardening

## Explicit Out of Scope for This Bootstrap

Do not add these in v1 unless explicitly requested later:

- shell context menu integration
- recycle bin support
- file permissions editor
- alternate data stream tools
- USN journal features
- hashing tools
- archive browsing
- FTP/SFTP/cloud backends
- tabs
- plugin system
- thumbnail generation

## Acceptance Checklist for Agent

The implementation is not complete until all of the following are true:

- solution and project names match this document
- package management is centralized
- .NET 10 is used
- CsWin32 is used for Win32 interop
- one file per type is respected
- dual-pane UI exists
- NTFS-only policy is enforced
- FileId is shown in properties or listing support model
- non-NTFS volumes are ignored
- long paths are supported
- favourites persist
- parallelization switch exists
- all implemented commands have TUnit integration tests
- architecture dependency rules are respected

## Reference Notes for Agent

Use official sources for any package or SDK version uncertainty.
Prefer stable channels.
Do not silently replace WinUI 3 with another UI framework.
Do not silently replace CsWin32 with handwritten DllImport.
Do not collapse multiple types into fewer files.
