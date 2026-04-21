# Spec: NuGet Modernization

Scope: package additions, swaps, and interop expansions that modernize the codebase. Excluded by user request: `Vanara.PInvoke.*`. Included by user preference: continue with `Microsoft.Windows.CsWin32` for all new native interop.

Constraints:
- Central package management is already on (`Directory.Packages.props`).
- Analyzer packages are covered separately in `SPEC_TOOLING_AND_ANALYZERS.md`; that spec must land first.
- Every addition must have a concrete callsite in this spec — no speculative dependencies.

Ordering is chronological: do the top of the list first, it unblocks later items.

## 1. Expand CsWin32 to replace hand-rolled `DllImport`s

CsWin32 is already referenced in `WinUiFileManager.Interop` but is under-used: `NtfsFileIdentityService` and `WindowsShellService` still contain manual `[DllImport]` blocks.

### 1.1. Add the missing Win32 APIs to `NativeMethods.txt`

Append these lines to `src/WinUiFileManager.Interop/NativeMethods.txt`:

```
SHObjectProperties
ShellExecuteExW
SHELLEXECUTEINFOW
SHCreateItemFromParsingName
IFileIsInUse
CfGetPlaceholderStateFromAttributeTag
RmStartSession
RmRegisterResources
RmGetList
RmEndSession
RM_PROCESS_INFO
CoInitializeEx
CoUninitialize
RegNotifyChangeKeyValue
REG_NOTIFY_CHANGE_LAST_SET
```

`RegNotifyChangeKeyValue` is required by `ILongPathsEnvironment` (see `SPEC_LONG_PATHS.md` §8.1) to react to external edits of the `LongPathsEnabled` registry value.

CsWin32 generates `PInvoke.*` wrappers + strongly-typed `SafeHandle`s, constants, and typed enums. Remove the equivalent manual definitions from:
- `src/WinUiFileManager.Infrastructure/Services/WindowsShellService.cs:101-157`
- `src/WinUiFileManager.Infrastructure/FileSystem/NtfsFileIdentityService.cs:809-960`
- `src/WinUiFileManager.Interop/Adapters/FileIdentityInterop.cs:372-469`

### 1.2. Move generated interop into the Interop project

All CsWin32 output lives in `WinUiFileManager.Interop`. Infrastructure must not import `Windows.Win32.*` directly; route every native call through an `IXxxInterop` abstraction. Keep the seam thin:

```
WinUiFileManager.Interop
  Adapters/
    IShellInterop.cs          (new)
    ShellInterop.cs           (moves SHObjectProperties + ShellExecuteExW out of Infrastructure)
    IRestartManagerInterop.cs (new)
    RestartManagerInterop.cs  (moves RM_* calls out of Adapters/FileIdentityInterop.cs)
    ICloudFilesInterop.cs     (new)
    CloudFilesInterop.cs      (moves CfGetPlaceholderStateFromAttributeTag)
```

`WindowsShellService`, `NtfsFileIdentityService`, and `FileIdentityInterop` shrink to orchestration logic.

### 1.3. CsWin32-generated `SafeHandle`s

Replace raw `IntPtr` file handles with the generated safe wrappers. `NtfsFileIdentityService.OpenMetadataHandle` already returns `SafeFileHandle`; extend that pattern to `FindFirstStreamW`/`FindClose` so the stream-enumeration path no longer uses `IntPtr`+`try/finally`.

### 1.4. Acceptance

- `grep "DllImport" src/` returns zero matches outside `NativeMethods.txt`-driven files.
- All Win32 constants (`FILE_READ_ATTRIBUTES`, `FILE_FLAG_BACKUP_SEMANTICS`, `ERROR_SUCCESS`, etc.) come from `Windows.Win32.*` namespaces.
- Existing tests continue to pass without modification (interfaces unchanged from the caller's perspective).

## 2. Upgrade CopyFile path to `CopyFile2` with a progress callback

Currently `FileOperationInterop.CopyFile` delegates to `File.Copy`. For 100K-file copies that's fine; for a single 10 GB file the user cannot cancel mid-copy.

### 2.1. Add to `NativeMethods.txt` (already present)

`CopyFile2` and `COPYFILE2_EXTENDED_PARAMETERS` are already listed. Wire up usage.

### 2.2. New overload

Extend `IFileOperationInterop.CopyFile` with an optional progress callback:

```csharp
InteropResult CopyFile(
    string source,
    string destination,
    bool overwrite,
    IProgress<long>? bytesCopied,
    CancellationToken cancellationToken);
```

Inside the implementation, use `PInvoke.CopyFile2` with a `PCOPYFILE2_PROGRESS_ROUTINE` callback that checks the token and returns `COPYFILE2_PROGRESS_CANCEL`. Fall back to `File.Copy` when no progress subscriber — keeps the fast path unchanged.

### 2.3. Acceptance

- Cancelling a multi-GB copy from the progress dialog aborts within 500 ms (previously had to wait for the whole file).
- No regression on the 100K small-file copy case.

## 2b. `CommunityToolkit.WinUI.Controls.Sizer` for smooth splitters

### 2b.1. Package

```xml
<PackageVersion Include="CommunityToolkit.WinUI.Controls.Sizer" Version="8.*" />
```

Reference from `WinUiFileManager.Presentation` only.

### 2b.2. Call sites

The two splitters in `MainShellView.xaml` (left-right panes and right-pane/inspector) use this control. See `SPEC_UI_LAYOUT_AND_RESIZING.md` §3.3 for wire-up details.

The Sizer uses a ghost-preview drag model: the layout commits only on `PointerReleased`, so the pane grids don't re-measure during drag. This is the single biggest resize-performance fix in the codebase.

### 2b.3. Acceptance

- Both splitters use `controls:Sizer`; the hand-rolled `PointerPressed/Moved/Released` handlers for inspector resize are removed.
- Drag is frame-smooth at 60 Hz even with a 100 000-file folder open. See `SPEC_UI_LAYOUT_AND_RESIZING.md` §8.1.

## 3. `Microsoft.Toolkit.HighPerformance` for pooled string / span utilities

### 3.1. Package

```xml
<PackageVersion Include="CommunityToolkit.HighPerformance" Version="8.*" />
```

Reference from `WinUiFileManager.Presentation` and `WinUiFileManager.Infrastructure`.

### 3.2. Call sites

- **`StringPool.Shared`** for file extensions. In `WindowsFileSystemService.BuildEntryModel(ref FileSystemEntry)` (line 160-178), extensions are highly redundant across a folder (`.txt`, `.cs`, etc.). `StringPool.Shared.GetOrAdd(name.AsSpan(lastDot))` dedupes without `ToString()` allocation until a miss.
- **`SpanOwner<T>`** in `FileIdentityInterop.TryGetRestartManagerLocks` (line 181) instead of `new RmProcessInfo[processInfoNeeded]` — reduces allocation on the "is this file locked" hot path (inspector queries it per selection).
- **`Box<T>`** not needed here; skip.

### 3.3. Acceptance

- Navigate to a 100 000-file folder. `dotnet-counters` shows < 40 MB heap size after the load. Without `StringPool` the baseline is ~60-80 MB due to duplicate extension strings.

## 4. `Nerdbank.Streams` for thumbnail byte handling

### 4.1. Package

```xml
<PackageVersion Include="Nerdbank.Streams" Version="2.*" />
```

Reference from `WinUiFileManager.Infrastructure`.

### 4.2. Call site

`NtfsFileIdentityService.GetThumbnailDiagnosticsAsync` (line 293-334) copies the thumbnail stream into a `MemoryStream` and calls `ToArray()`. For many thumbnails per selection, the intermediate byte array allocates + GCs a full image each time.

Replace with a pooled `Sequence<byte>` and a `ReadOnlySequence<byte>` return type, or simply reuse a single pooled `byte[]` rented from `ArrayPool<byte>.Shared`:

```csharp
using var lease = MemoryPool<byte>.Shared.Rent(64 * 1024);
await using var memory = new Sequence<byte>();
await input.CopyToAsync(memory.AsStream(), cancellationToken);
thumbnailBytes = memory.AsReadOnlySequence.ToArray(); // unavoidable if callers want byte[]
```

Better: change `FileThumbnailDiagnosticsDetails.ThumbnailBytes` to `ReadOnlyMemory<byte>` and have the inspector consume that directly. `InMemoryRandomAccessStream.WriteAsync` accepts a WinRT `IBuffer` which can be wrapped from `ReadOnlyMemory<byte>` via `WindowsRuntimeBufferExtensions.AsBuffer()`.

### 4.3. Acceptance

- Arrow-scroll through 5 000 files with thumbnails on. `dotnet-counters` `gc-heap-size` growth under 10 MB over the run.

## 5. Structured logging with Serilog

### 5.1. Packages

```xml
<PackageVersion Include="Serilog" Version="4.*" />
<PackageVersion Include="Serilog.Extensions.Logging" Version="8.*" />
<PackageVersion Include="Serilog.Sinks.File" Version="6.*" />
<PackageVersion Include="Serilog.Sinks.Async" Version="2.*" />
<PackageVersion Include="Serilog.Sinks.Debug" Version="3.*" />
<PackageVersion Include="Serilog.Enrichers.Process" Version="3.*" />
<PackageVersion Include="Serilog.Enrichers.Thread" Version="4.*" />
```

Reference from `WinUiFileManager.App` only; other projects keep depending on `Microsoft.Extensions.Logging.Abstractions`.

### 5.2. Wiring

In `ServiceConfiguration.ConfigureServices`:

```csharp
var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "WinUiFileManager", "logs", "app-.log");

var serilog = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Async(a => a.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"))
    .WriteTo.Debug()
    .CreateLogger();

services.AddLogging(builder => builder.AddSerilog(serilog, dispose: true));
```

Use `Serilog.Sinks.Async` so file writes never block the logging call.

### 5.3. Dovetails with `LoggerMessage` source generation

The analyzer spec introduces `LoggerMessage`-generated loggers. Those emit standard `Microsoft.Extensions.Logging` calls — Serilog's structured property model captures them correctly. No double setup needed.

### 5.4. Acceptance

- App start creates `%LocalAppData%\WinUiFileManager\logs\app-YYYYMMDD.log`.
- A copy operation logs a single structured event per operation start/complete, not per file.
- File size stays under 5 MB/day for normal use.

## 6. `CommunityToolkit.Mvvm` — keep but leverage source generators fully

Already referenced. Tighten usage:
- Use `[ObservableProperty]` with the new `partial` syntax (already the pattern).
- Use `[NotifyPropertyChangedFor(nameof(SomeDerived))]` on every `[ObservableProperty]` whose change invalidates a computed property (e.g., `FileInspectorFieldViewModel.OnValueChanged` → use the attribute instead of manual `OnPropertyChanged`).
- Use `[RelayCommand(AllowConcurrentExecutions = false)]` on command methods that must not re-entry (copy/move/delete) instead of hand-rolling `if (OperationProgress.IsRunning) return;` guards.

No package change; this is just consistent usage.

## 7. `System.IO.Pipelines` for thumbnail byte copy

Built into the runtime (no NuGet). Same motivation as item 4 but without an extra dependency. If `Nerdbank.Streams` is rejected as an external dependency, use `PipeReader.Create(input)` plus `ReadResult.Buffer.ToArray()`.

Decision point: `Nerdbank.Streams` wins if the same pattern is needed elsewhere (e.g., a future hashing feature) because it ships `Sequence<T>` which is reusable. If only the thumbnail path uses pooled buffers, `ArrayPool<byte>.Shared` alone (runtime) is enough.

**Recommendation:** start with `ArrayPool<byte>.Shared` (no new dependency), promote to `Nerdbank.Streams` when a second call site appears.

## 8. `DynamicData` — keep, tighten

Already referenced. Tightenings:

- Use `SortAndBind` overloads that accept `IObservable<IComparer<T>>` (already done) and `SortOptions.ResortOnSourceRefresh: false` to avoid re-sorting on no-op change-sets.
- Use `Change.Transform` with a `static` lambda (CodingStyle rule 49-52) — current transforms are already static-capable; audit and mark.
- Replace `SourceCache<T,TKey>.Edit(updater => { updater.Remove(...); updater.AddOrUpdate(...); })` with `SourceCache<T,TKey>.EditDiff(...)` when the new collection is the full authoritative set (refresh scenarios). Produces minimal deltas automatically.

No package change.

## 9. `Microsoft.Reactive.Testing` — keep

Already present (`Directory.Packages.props`). Used by the watcher test introduced in `SPEC_BUG_FIXES.md` B1 and by future perf regression tests. No change.

## 10. Optional: `OpenTelemetry` metrics (defer)

Out of scope for initial modernization. Revisit when:
- The app is deployed beyond the current team.
- A central dashboard (Grafana, App Insights) is available.

If added later:
- `OpenTelemetry` + `OpenTelemetry.Extensions.Hosting`.
- Export file-operation duration, item count, cancellation rate as `Meter` counters.
- Use `System.Diagnostics.Metrics` in the command handlers; OTel picks them up automatically.

## 11. Packages to remove

None. The current manifest is lean. Leave it.

## 12. Delivery order

1. Expand CsWin32 (§1) — purely mechanical, unblocks everything.
2. **`CommunityToolkit.WinUI.Controls.Sizer` (§2b)** — pulled forward because `SPEC_UI_LAYOUT_AND_RESIZING.md` is scheduled next and depends on it.
3. Structured logging with Serilog (§5) — improves visibility before subsequent work.
4. `ArrayPool`/`StringPool` via `CommunityToolkit.HighPerformance` (§3).
5. CopyFile2 upgrade (§2) — adds cancel responsiveness.
6. Thumbnail pipeline tightening (§4 or §7).
7. DynamicData usage audit (§8).

Each step lands independently; none depends on the next (except step 2, which must land before the UI-layout spec begins).

## 13. Acceptance (overall)

- `Directory.Packages.props` contains only packages with at least one concrete callsite.
- No `[DllImport]` outside of CsWin32-driven generation.
- Log files appear under `%LocalAppData%\WinUiFileManager\logs\`.
- `dotnet-counters` shows improved steady-state allocation after the string/byte pooling work (concrete numbers in `SPEC_PERFORMANCE_AND_SCALE.md`).
