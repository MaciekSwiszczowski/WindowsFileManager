# Developer Tooling Guide

This document explains the NuGet packages, technical patterns, and development practices added during the tooling batches in this thread. It is intentionally brief and practical.

For the full prescriptive baseline, see [SPEC_TOOLING_AND_ANALYZERS.md](/C:/_github/WindowsFileManager/WindowsFileManager/docs/SPEC_TOOLING_AND_ANALYZERS.md).

## Goals

The tooling work in this thread had four goals:

1. Catch bugs earlier at build time.
2. Enforce a few architectural rules automatically.
3. Narrow implementation visibility so the codebase exposes less by default.
4. Keep diagnostics and startup validation cheap and reliable.

## NuGet Packages Added

### `Meziantou.Analyzer`

Broad code-quality analyzer pack.

Why it helps:
- Catches general hygiene issues that the compiler does not.
- Good at flagging cancellation, disposal, and common async mistakes.

What to remember:
- Treat it as the “wide net” analyzer.

### `Microsoft.VisualStudio.Threading.Analyzers`

Threading and async correctness analyzers.

Why it helps:
- Flags sync-over-async mistakes such as `.Result` and `.Wait()`.
- Pushes code toward correct async naming and await usage.

What to remember:
- If this analyzer complains, the code is usually risking deadlocks or UI-thread stalls.

### `ErrorProne.NET.CoreAnalyzers`

Performance- and correctness-focused analyzer pack.

Why it helps:
- Detects accidental allocations, struct copies, and closure mistakes.
- Useful in hot paths and repeated callbacks.

What to remember:
- This one is especially valuable in UI update loops and file-system pipelines.

### `Roslynator.Analyzers`

Low-noise refactoring and correctness analyzers.

Why it helps:
- Surfaces cleanup opportunities and a number of safe modernizations.
- Helps keep code simpler as the project grows.

What to remember:
- Usually a “make this code cleaner” signal rather than a behavioral warning.

### `IDisposableAnalyzers`

Disposal lifetime analyzer.

Why it helps:
- This app uses Rx, caches, watchers, cancellation tokens, and native handles.
- Those patterns are easy to leak accidentally.

What to remember:
- If ownership of a disposable object is unclear, assume the analyzer is protecting you from a real bug.

### `AsyncFixer`

Additional async analyzer rules.

Why it helps:
- Flags unnecessary `async` wrappers.
- Flags fire-and-forget patterns that often hide lost exceptions.

What to remember:
- Prefer returning the task directly unless you need `await`.

### `Microsoft.CodeAnalysis.BannedApiAnalyzers`

Architectural rule enforcement through a banned-symbol list.

Why it helps:
- Stops developers from bypassing the approved abstractions.
- Makes bad patterns fail the build instead of surviving review.

What to remember:
- If you hit `RS0030`, do not suppress it casually. Usually the fix is “use the approved service/interface instead”.

## Technical Patterns Added

### Central analyzer wiring

Files:
- [Directory.Packages.props](/C:/_github/WindowsFileManager/WindowsFileManager/Directory.Packages.props)
- [Directory.Build.props](/C:/_github/WindowsFileManager/WindowsFileManager/Directory.Build.props)

What it means:
- Package versions are defined once.
- Analyzer references and warning policy are applied to every project consistently.

Why this is good:
- No per-project drift.
- New projects inherit the same engineering floor automatically.

### `.editorconfig` as a build rule source

File:
- [.editorconfig](/C:/_github/WindowsFileManager/WindowsFileManager/.editorconfig)

What it means:
- Formatting and style are not just IDE preferences anymore.
- Some style choices now fail CI or `dotnet format`.

Why this is good:
- The repo gets one shared definition of “clean”.

### Banned API list

Files:
- [BannedSymbols.txt](/C:/_github/WindowsFileManager/WindowsFileManager/BannedSymbols.txt)
- [BannedSymbols.Interop.txt](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Interop/BannedSymbols.Interop.txt)

What it means:
- Certain APIs are intentionally disallowed, such as `Task.Wait`, `Task.Result`, direct `File.Copy`, direct `File.Move`, and ad-hoc `DllImport`.

Why this is good:
- It forces file operations, native interop, and threading-sensitive code through the project’s chosen boundaries.

Important nuance:
- Interop uses a project-specific banned-symbol file because CsWin32 generates `DllImport`-based code internally, and that generated surface cannot be waived cleanly with file pragmas.

### File-scoped suppressions for legitimate exceptions

What it means:
- We only suppress analyzer rules where the code is the approved boundary.

Examples from this thread:
- `FileOperationInterop` for approved direct file-operation calls.
- `RxSchedulerProvider` for the one allowed `SynchronizationContext.Current` capture.
- legacy native call sites that are quarantined until full CsWin32 migration.

Why this is good:
- Exceptions remain visible and reviewable.
- We avoid global “just disable the rule” shortcuts.

### `InternalsVisibleTo` + internal-by-default implementations

Files:
- library `.csproj` files under `src/`

What it means:
- Tests can still inspect implementation code.
- Production code exposes fewer concrete types publicly.

Why this is good:
- Public API surface stays smaller.
- Fewer implementation details leak across assembly boundaries.

Rule of thumb:
- Interfaces and true contracts stay `public`.
- Concrete implementation classes should be `internal` unless another production assembly must construct them directly.

### Debug DI validation

File:
- [ServiceConfiguration.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.App/Composition/ServiceConfiguration.cs)

What it means:
- In Debug builds, `BuildServiceProvider` validates registrations and scopes at startup.

Why this is good:
- Missing registrations fail early.
- Bad lifetime graphs are caught before they become runtime bugs.

Rule of thumb:
- If you add a new service and startup breaks in Debug, fix the registration graph instead of weakening validation.

### `LoggerMessage` source-generated logging

Files:
- [FilePaneViewModelLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Presentation/ViewModels/FilePaneViewModelLog.cs)
- [WindowsFileSystemServiceLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/FileSystem/WindowsFileSystemServiceLog.cs)
- [WindowsFileOperationServiceLog.cs](/C:/_github/WindowsFileManager/WindowsFileManager/src/WinUiFileManager.Infrastructure/Execution/WindowsFileOperationServiceLog.cs)

What it means:
- Hot-path logs are defined as generated methods instead of inline `LogInformation` / `LogError` calls.

Why this is good:
- Lower logging overhead.
- Stable event IDs.
- Strongly-typed structured logging fields.
- Cleaner runtime code.

Rule of thumb:
- Use `LoggerMessage` for repeated or hot-path logs.
- Inline logging is still fine for cold, one-off paths.

## Practices Reinforced

### Build with warnings as errors

What it means:
- A warning in the enforced rule set is treated as a failed build.

Why this is good:
- Prevents “we will clean it up later” drift.

### Verify formatting in tooling, not by taste

Command used:
- `dotnet format --verify-no-changes`

Why this is good:
- Formatting becomes objective and repeatable.

### Prefer explicit boundaries over convenience APIs

Examples:
- Use `IFileOperationInterop` instead of direct `File.Copy` / `File.Move`.
- Use DI abstractions instead of newing production services in app code.
- Route native APIs through `NativeMethods.txt` and CsWin32 where possible.

Why this is good:
- Centralizes policy, error handling, cancellation, and future upgrades.

### Keep exceptions narrow and documented

What it means:
- If a rule must be waived, do it at file scope with a reason.

Why this is good:
- Future developers can see that the exception is intentional, not accidental.

## Practical Advice for Contributors

When adding new code:

- Start with the existing abstraction before reaching for a framework API directly.
- Assume implementation classes should be `internal` unless there is a clear contract reason otherwise.
- If you add a repeated log in a busy path, prefer `LoggerMessage`.
- If a new analyzer fires, fix the code first and suppress only if the code is an approved boundary.
- Run:
  - `dotnet build WinUiFileManager.sln -c Debug -p:Platform=x64`
  - `dotnet build WinUiFileManager.sln -c Release -p:Platform=x64`
  - `dotnet test`
  - `dotnet format --verify-no-changes WinUiFileManager.sln`

## Related Files

- [Directory.Build.props](/C:/_github/WindowsFileManager/WindowsFileManager/Directory.Build.props)
- [Directory.Packages.props](/C:/_github/WindowsFileManager/WindowsFileManager/Directory.Packages.props)
- [.editorconfig](/C:/_github/WindowsFileManager/WindowsFileManager/.editorconfig)
- [BannedSymbols.txt](/C:/_github/WindowsFileManager/WindowsFileManager/BannedSymbols.txt)
- [SPEC_TOOLING_AND_ANALYZERS.md](/C:/_github/WindowsFileManager/WindowsFileManager/docs/SPEC_TOOLING_AND_ANALYZERS.md)
- [tooling-batch-3.md](/C:/_github/WindowsFileManager/WindowsFileManager/docs/progress/tooling-batch-3.md)
