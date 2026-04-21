# Spec: Tooling and Analyzer Baseline

Scope: the must-have tooling and analyzer upgrades. Everything here is build-/CI-level and does not change runtime behavior. Ship this before any of the other specs so the subsequent fixes are guarded by static analysis.

Constraints from `AGENT_BRIEF.md` and `CODING_STYLE.md` are assumed and not re-stated.

## 1. Analyzer NuGet packages

### 1.1. Must-have, applied to every project

Add to `Directory.Packages.props` (central versioning is already enabled):

```xml
<PackageVersion Include="Meziantou.Analyzer" Version="2.*" />
<PackageVersion Include="Microsoft.VisualStudio.Threading.Analyzers" Version="17.*" />
<PackageVersion Include="ErrorProne.NET.CoreAnalyzers" Version="0.6.*" />
<PackageVersion Include="Roslynator.Analyzers" Version="4.*" />
<PackageVersion Include="IDisposableAnalyzers" Version="4.*" />
<PackageVersion Include="AsyncFixer" Version="1.*" />
<PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.*" />
```

Reference them from **every** project via `Directory.Build.props` so they apply uniformly:

```xml
<ItemGroup>
  <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
  <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" PrivateAssets="all" />
  <PackageReference Include="ErrorProne.NET.CoreAnalyzers" PrivateAssets="all" />
  <PackageReference Include="Roslynator.Analyzers" PrivateAssets="all" />
  <PackageReference Include="IDisposableAnalyzers" PrivateAssets="all" />
  <PackageReference Include="AsyncFixer" PrivateAssets="all" />
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
</ItemGroup>
```

Rationale (unique value of each; purposeful overlap is deliberate to raise the floor):

- **Meziantou.Analyzer** — broad hygiene: `async void`, `ObservableCollection` misuse, `ConfigureAwait`, unused `CancellationToken`, disposal, LINQ ordering mistakes.
- **Microsoft.VisualStudio.Threading.Analyzers** — `VSTHRD100` (`async void`), `VSTHRD103` (sync blocking on async), `VSTHRD110` (missing await), `VSTHRD200` (async naming). Comprehensive threading-specific coverage.
- **ErrorProne.NET.CoreAnalyzers** — closure captures in hot lambdas, struct copies on property access, disposable field tracking, exception-message localisability.
- **Roslynator.Analyzers** — refactor-quality rules that Meziantou does not emit (RCS1090 `ConfigureAwait`, RCS1236 exception filters, many simplify/invert/merge suggestions). Low false-positive rate.
- **IDisposableAnalyzers** — *the* analyzer for this codebase. Tracks `IDisposable` lifetimes across `SourceCache<,>`, `BehaviorSubject<T>`, Rx subscriptions, `CancellationTokenSource`, `FileSystemWatcher`, `SafeFileHandle`, `InMemoryRandomAccessStream`. The review already identified ownership bugs here (`MainShellViewModel.Dispose` never called, `FileInspectorViewModel.Dispose` a no-op); this analyzer catches those and prevents regressions.
- **AsyncFixer** — unique rules beyond VSTHRD: `AF0001` (unnecessary `async`/`await`), `AF0004` (fire-and-forget detection in UI event handlers).
- **Microsoft.CodeAnalysis.BannedApiAnalyzers** — architectural enforcement; see §1.3 for the `BannedSymbols.txt` content.

Do NOT add:
- **StyleCop.Analyzers** — conflicts with ReSharper defaults per `CODING_STYLE.md`.
- **Microsoft.CodeAnalysis.PublicApiAnalyzers** — this is an app, not a library with an API stability contract.
- **SecurityCodeScan** — overkill for an internal NTFS-only dev tool without network, auth, or crypto.

### 1.2. Optional, CI-only

If build-time noise in the IDE is acceptable later, consider promoting these from CI to IDE. Keep them CI-only for now:

```xml
<PackageVersion Include="SonarAnalyzer.CSharp" Version="10.*" />
<PackageVersion Include="Roslyn.Diagnostics.Analyzers" Version="3.*" />
```

Gate in `Directory.Build.props` so they only run on CI:

```xml
<ItemGroup Condition="'$(CI)' == 'true'">
  <PackageReference Include="SonarAnalyzer.CSharp" PrivateAssets="all" />
  <PackageReference Include="Roslyn.Diagnostics.Analyzers" PrivateAssets="all" />
</ItemGroup>
```

- **SonarAnalyzer.CSharp** — excellent additional null-ref analysis, cognitive-complexity caps, duplicate-code detection. Noisy, but CI-only means it gates merges without distracting live edits.
- **Roslyn.Diagnostics.Analyzers** — Microsoft's internal ruleset from the Roslyn project itself; focus on allocation hygiene and test patterns. Useful for the perf-sensitive paths identified in the review.

### 1.3. Banned API list (architectural enforcement)

Create `BannedSymbols.txt` in the repo root (or under `build/`) and reference it from `Directory.Build.props`:

```xml
<ItemGroup>
  <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
</ItemGroup>
```

Initial `BannedSymbols.txt`:

```
# Sync-over-async — see SPEC_BUG_FIXES.md B5.
M:System.Threading.Tasks.Task.get_Result;Use await.
M:System.Threading.Tasks.Task`1.get_Result;Use await.
M:System.Threading.Tasks.Task.Wait;Use await.
M:System.Threading.Tasks.Task.Wait(System.Int32);Use await.
M:System.Threading.Tasks.Task.Wait(System.Threading.CancellationToken);Use await.

# Force file operations through IFileOperationInterop (CopyFile2 path with cancellation).
M:System.IO.File.Copy(System.String,System.String);Route through IFileOperationInterop.CopyFile.
M:System.IO.File.Copy(System.String,System.String,System.Boolean);Route through IFileOperationInterop.CopyFile.
M:System.IO.File.Move(System.String,System.String);Route through IFileOperationInterop.MoveFile.
M:System.IO.File.Move(System.String,System.String,System.Boolean);Route through IFileOperationInterop.MoveFile.

# Force volume queries through INtfsVolumePolicyService (cached) — see SPEC_BUG_FIXES.md B6.
P:System.IO.DriveInfo.DriveFormat;Route through INtfsVolumePolicyService (cached).
P:System.IO.DriveInfo.VolumeLabel;Route through INtfsVolumePolicyService (cached).

# Every new native call must go through NativeMethods.txt (CsWin32). See SPEC_NUGET_MODERNIZATION.md §1.
T:System.Runtime.InteropServices.DllImportAttribute;Add the API to NativeMethods.txt and use the CsWin32-generated PInvoke wrapper.

# SynchronizationContext.Current is captured eagerly by RxSchedulerProvider — direct use elsewhere is a trap.
P:System.Threading.SynchronizationContext.Current;Inject DispatcherQueue or ISchedulerProvider explicitly.

# No hand-rolled GC in shipped code.
M:System.GC.Collect;Remove. Measure with dotnet-counters instead.
M:System.GC.Collect(System.Int32);Remove. Measure with dotnet-counters instead.
M:System.GC.Collect(System.Int32,System.GCCollectionMode);Remove. Measure with dotnet-counters instead.
```

Suppression surface: legitimate exceptions (e.g., `File.Copy` inside `FileOperationInterop` itself, `DllImport` inside CsWin32-generated files) get a file-scoped `#pragma warning disable RS0030` with a one-line justification. The reviewer sees every waiver.

Extend the list over time when new architectural rules are discovered; every addition must cite a spec or incident.

## 2. `Directory.Build.props` changes

Replace the current `<AnalysisLevel>latest</AnalysisLevel>` block with:

```xml
<PropertyGroup>
  <AnalysisMode>Recommended</AnalysisMode>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <EnableNETAnalyzers>true</EnableNETAnalyzers>

  <!-- async / threading hygiene, fail fast -->
  <WarningsAsErrors>
    CA2007;CA2012;CA2016;CA1849;
    VSTHRD100;VSTHRD103;VSTHRD110;VSTHRD200;
    MA0040;MA0041;MA0134;
    AF0001;AF0004;
    CS1998;
    <!-- IDisposable lifetime tracking -->
    IDISP001;IDISP003;IDISP004;IDISP007;
    <!-- Banned APIs: architectural enforcement -->
    RS0030
  </WarningsAsErrors>

  <!-- LoggerMessage source generator is a nice-to-have but not a build failure -->
  <NoWarn>CA1848</NoWarn>
</PropertyGroup>
```

Codes used:
- `CA2007` — missing `ConfigureAwait(false)` in library code.
- `CA2012` — `ValueTask` awaited more than once.
- `CA2016` — not forwarding `CancellationToken`.
- `CA1849` — sync I/O call in async method.
- `VSTHRD100` — `async void`.
- `VSTHRD103` — `.Result`/`.Wait()` on async.
- `VSTHRD110` — awaitable not awaited.
- `VSTHRD200` — `*Async` naming.
- `MA0040` — pass available `CancellationToken`.
- `MA0041` — use overload with `CancellationToken`.
- `MA0134` — avoid `Task.Wait`, `Task.Result`.
- `CS1998` — `async` method without `await`.

Exemption surface: WinUI 3 XAML event handlers are legitimately `async void`. Add file-scoped suppressions via `#pragma warning disable VSTHRD100` at the handler method, **not** a blanket suppression. Reviewer must see the waiver.

## 3. `.editorconfig`

Create `.editorconfig` at the repo root (or extend if present) with the rules that ReSharper does not enforce at build time:

```ini
root = true

[*.cs]
# Per CODING_STYLE.md — always braces
csharp_prefer_braces = true:error

# Narrow visibility
dotnet_style_require_accessibility_modifiers = always:warning

# Pattern matching preferred
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# Static lambdas required for non-capturing lambdas
csharp_style_prefer_static_anonymous_function = true:warning

# Simplify using
dotnet_diagnostic.IDE0005.severity = warning

# Prefer readonly fields
dotnet_style_readonly_field = true:warning

# Prefer `is null` / `is not null`
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_null_propagation = true:suggestion

# No tab/space disagreements
indent_style = space
indent_size = 4
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true
```

## 4. DI validation in Debug

`App.xaml.cs` currently builds the service provider unconditionally. In Debug, add validation:

```csharp
#if DEBUG
    _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateOnBuild = true,
        ValidateScopes = true,
    });
#else
    _serviceProvider = services.BuildServiceProvider();
#endif
```

Catches transient-into-singleton captures and missing registrations at startup.

## 5. `InternalsVisibleTo` for tests

Every library currently exposes its implementation types as `public` solely so tests can reach them. `CODING_STYLE.md` requires the narrowest valid accessibility. Add to each library’s csproj:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="WinUiFileManager.Application.Tests" />
  <InternalsVisibleTo Include="WinUiFileManager.Infrastructure.Tests" />
  <InternalsVisibleTo Include="WinUiFileManager.Interop.Tests" />
</ItemGroup>
```

Change **types only needed within their owning library** from `public` to `internal`. Start with:
- `WinUiFileManager.Infrastructure.Persistence.FavouriteDto`
- `WinUiFileManager.Infrastructure.Persistence.SettingsDto`
- `WinUiFileManager.Infrastructure.FileSystem.WindowsFileSystemService` (and the sibling Service classes) — they only need to be resolvable via DI; DI works with `internal` when composition root is in a friend assembly. Adjust `AddInfrastructureServices` registrations to use the concrete types by namespace.
- `WinUiFileManager.Interop.Adapters.FileOperationInterop`, `FileIdentityInterop`, `VolumeInterop` — only the interfaces need to be `public` (and only if consumed cross-assembly; they are, since Application references the interfaces via Abstractions).

Interfaces in `WinUiFileManager.Application.Abstractions` and `WinUiFileManager.Domain.*` can remain `public` — they are the assembly’s contract.

## 6. Source-generated logging

Replace interpolated log messages with `LoggerMessage` source-generated partial classes. Pattern:

```csharp
internal static partial class PaneLog
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Debug,
        Message = "Pane load canceled for {Path}")]
    public static partial void PaneLoadCanceled(
        ILogger logger, string path);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error,
        Message = "Failed to load directory {Path}")]
    public static partial void LoadFailed(
        ILogger logger, Exception ex, string path);
}
```

Convert the hot-path logs in `FilePaneViewModel`, `WindowsDirectoryChangeStream`, `WindowsFileSystemService`, `WindowsFileOperationService`. Cold paths (one-offs) can stay interpolated.

Side benefit: structured properties are now typed, queryable, and allocation-free on disabled log levels.

## 7. Test infrastructure project

Create `tests/WinUiFileManager.Testing/` with:

- `TempDirectory` — `IAsyncDisposable` wrapping `Directory.CreateTempSubdirectory`. Cleans up in `DisposeAsync`.
- `LargeFolderBuilder(int count, long fileSizeBytes = 0)` — materializes empty files under a `TempDirectory`. Used by perf regression tests. Uses `Parallel.For` with `CreateFileW` through CsWin32 for throughput.
- `FakeFileSystemService : IFileSystemService` — in-memory tree for Application-layer tests so they don’t touch disk.
- `TestSchedulerProvider : ISchedulerProvider` — wraps `Microsoft.Reactive.Testing.TestScheduler` for deterministic Rx testing.
- `TUnitAssertionExtensions` — project-specific asserts (e.g., `AssertNoAllocationsGreaterThan`).

Reference it via `<ProjectReference>` from every test project.

## 8. CI checklist

Add or extend a CI workflow that:

1. Runs `dotnet restore` with `--locked-mode` once a lock file exists.
2. Runs `dotnet build -warnaserror` (the project-level setting already covers this; enforce the flag in case a dev disables it locally).
3. Runs `dotnet test` for all test projects.
4. Runs `dotnet format --verify-no-changes` to enforce `.editorconfig`.
5. Checks package health: `dotnet list package --outdated` — informational, not blocking.

Do not add benchmarks to the blocking CI; they belong to the nightly run (see the perf spec).

## 9. Release-build posture

Add to each executable project (only `WinUiFileManager.App` today):

```xml
<PropertyGroup Condition="'$(Configuration)'=='Release'">
  <PublishReadyToRun>true</PublishReadyToRun>
  <TieredCompilation>true</TieredCompilation>
  <TieredPGO>true</TieredPGO>
</PropertyGroup>
```

Trimming and AOT are out of scope: WinUI 3 XAML reflection is not trimmer-clean in SDK 1.8.

## 10. Acceptance

This spec is complete when:

- `dotnet build -c Release` succeeds with zero warnings on a clean repo.
- `dotnet format --verify-no-changes` succeeds.
- `dotnet test` passes (no behavioural change expected).
- At least one `[LoggerMessage]` exists and is exercised by a test.
- `WinUiFileManager.Testing` compiles and is referenced by all three existing test projects.
- DI validation is active in Debug and passes (no startup exception).
- `grep "async void"` outside `*.xaml.cs` files returns zero matches (it is OK on XAML event handlers; flag anything else).
