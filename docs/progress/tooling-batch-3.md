# Batch 3 of 3: Visibility, Logging, and DI Validation

**Spec:** `SPEC_TOOLING_AND_ANALYZERS.md` §4, §5, §6
**Branch merged into main:** `master`
**Status:** complete

## What shipped
- Added `InternalsVisibleTo` entries to the library projects and narrowed the infrastructure/interop implementation classes from `public` to `internal`, keeping only the interface contracts public.
- Enabled Debug DI validation in [ServiceConfiguration.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.App/Composition/ServiceConfiguration.cs:14) with `ValidateOnBuild` and `ValidateScopes`.
- Replaced the hottest logging sites with `LoggerMessage`-generated helpers in [FilePaneViewModelLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModelLog.cs:1), [WindowsFileSystemServiceLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/FileSystem/WindowsFileSystemServiceLog.cs:1), and [WindowsFileOperationServiceLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Execution/WindowsFileOperationServiceLog.cs:1).
- Added a service-graph validation test in [ServiceRegistrationValidationTests.cs](/C:/_github/WindowsFileManager/WindowsFileManager/tests/WinUiFileManager.Application.Tests/Scenarios/ServiceRegistrationValidationTests.cs:1) so the validated composition shape is exercised in tests.
- Cleaned the pre-existing formatting/encoding issues that blocked `dotnet format --verify-no-changes`.

## What's next
- Start `SPEC_NUGET_MODERNIZATION.md` batch `N-2b`: add `CommunityToolkit.WinUI.Controls.Sizer` to central package management and the Presentation project.
- Keep the Interop adapter visibility model intact: Infrastructure consumes those internal types through a friend-assembly relationship from Interop.

## Acceptance results
- [x] `dotnet build -c Release` succeeds with zero warnings on a clean repo.
- [x] `dotnet format --verify-no-changes` succeeds.
- [x] `dotnet test` passes for all three existing test projects.
- [x] At least one `[LoggerMessage]` exists and is exercised by a test.
- [skipped — out of scope for T-3] `WinUiFileManager.Testing` compiles and is referenced by all three existing test projects.
- [x] DI validation is active in Debug and passes.
- [skipped — not re-checked in this batch] `grep "async void"` outside `*.xaml.cs` files returns zero matches.

## Surprises
- Interop adapter classes could not be made `internal` with only test friend assemblies because Infrastructure owns their DI registration. The fix was to add `InternalsVisibleTo="WinUiFileManager.Infrastructure"` in the Interop project.
- `dotnet format --verify-no-changes` initially failed on several pre-existing whitespace, encoding, newline, and XML-doc issues outside the planned T-3 surface; those were fixed as part of closing the batch cleanly.

## Context hints for the next agent
- The visibility-narrowing pass touched these key areas: `src/WinUiFileManager.Infrastructure/*`, `src/WinUiFileManager.Interop/Adapters/*`, and the library `.csproj` files.
- The new logger helper classes live beside their owning runtime files and use event-id ranges `100`, `200`, and `300`.
- The Application test project now contains a DI validation scenario that mirrors the app’s composition using test doubles for UI-facing services.
