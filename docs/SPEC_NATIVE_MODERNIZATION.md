# Spec: Native Code Modernization

Scope: modernize the native / interop surface of the Windows File Manager with a **handle-safety first** posture. Every OS handle deterministically closed; every `[DllImport]` generated from `NativeMethods.txt`; every COM RCW released through the right API on the right thread; every native-boundary `Async` method honoring its cancellation token. Analyzer rules enforce each of these patterns at build time.

This spec is the authoritative source for native modernization. It **absorbs** the following items that were previously tracked elsewhere:

- `SPEC_NUGET_MODERNIZATION.md` §1 (CsWin32 expansion — was batch N-1).
- `SPEC_NUGET_MODERNIZATION.md` §2 (CopyFile2 upgrade — was batch N-4).
- `SPEC_BUG_FIXES.md` B3 (swallowed `OperationCanceledException`).
- `SPEC_BUG_FIXES.md` B9 (`Marshal.ReleaseComObject` insufficient for the COM RCW used by file-lock detection).

It explicitly **defers** these to their original homes (revisit after this spec lands):

- `SPEC_NUGET_MODERNIZATION.md` §3 (`CommunityToolkit.HighPerformance.StringPool`).
- `SPEC_NUGET_MODERNIZATION.md` §4 / §7 (thumbnail `ArrayPool<byte>` pooling).
- ComWrappers migration (considered, not required — modern `Marshal.FinalReleaseComObject` is sufficient for the single RCW we own).

Landing order: **right after the rename-bug spec** (`SPEC_RENAME_BUGS.md`) closes, before the keyboard-shortcut spec.

## 1. Goals

1. **Every OS handle wrapped in a `SafeHandle`** at the boundary where it is returned. No long-lived `IntPtr` fields in managed code outside `WinUiFileManager.Interop`.
2. **One auditable location for every `[DllImport]`** — `NativeMethods.txt`, processed by `Microsoft.Windows.CsWin32`. Zero hand-rolled imports in `src/`.
3. **Deterministic COM RCW disposal** using `Marshal.FinalReleaseComObject` in `finally`, on the thread that created the RCW, with the COM apartment's threading model documented at every call site.
4. **CancellationToken contract honored** at every `Async` boundary that touches native code — `OperationCanceledException` flows out instead of being swallowed.
5. **Compile-time enforcement** of the above via `IDisposableAnalyzers`, `Meziantou.Analyzer`, and `BannedSymbols.txt`.
6. **Audit checklist** every PR reviewer runs through when native code is touched (see §6).

## 2. Current native surface

Condensed from a read of the repository as of commit `35e965f`. `file:line` references are approximate — re-confirm when editing.

### 2.1. Hand-rolled `[DllImport]`s (targets for M-4)

- `src/WinUiFileManager.Interop/Adapters/FileIdentityInterop.cs` (≈ lines 372-398) — 5 imports: `RmStartSession`, `RmRegisterResources`, `RmGetList`, `RmEndSession` (Restart Manager), `SHCreateItemFromParsingName` (Shell COM binding).
- `src/WinUiFileManager.Infrastructure/Services/WindowsShellService.cs` (≈ lines 102-118) — 4 imports: `SHObjectProperties`, `ShellExecuteExW`, `CoInitializeEx`, `CoUninitialize`.
- `src/WinUiFileManager.Infrastructure/FileSystem/NtfsFileIdentityService.cs` (≈ lines 810-879) — 6 imports: `GetFileInformationByHandle`, `CreateFileW`, `GetFileInformationByHandleEx` (multiple `EntryPoint`s), `GetVolumeInformationW`, `GetFinalPathNameByHandleW`, `FindFirstStreamW`, `FindNextStreamW`, `FindClose`, `CfGetPlaceholderStateFromAttributeTag`.

All three files carry file-scoped `#pragma warning disable RS0030` with one-line justifications. These go away when M-4 lands.

### 2.2. COM RCW usage

`FileIdentityInterop.cs` ≈ line 451 — private `IFileIsInUseNative` interface with `[ComImport]` + `[Guid]`. Instantiated via `SHCreateItemFromParsingName` (≈ line 297). Currently released with `Marshal.ReleaseComObject(fileIsInUse)` (≈ line 345).

### 2.3. SafeHandle usage (already correct — keep)

- `NtfsFileIdentityService.OpenMetadataHandle()` (≈ line 359-377) wraps `CreateFileW`'s returned `IntPtr` in `SafeFileHandle`. Good.
- `FileIdentityInterop.GetFileIdFromHandle(SafeFileHandle safeHandle, …)` (≈ line 157-180) — consumer takes a `SafeFileHandle` and uses `DangerousGetHandle` inside a narrow block. Good.
- `FileStream` and `Process` usage — `using var` throughout. Good.

### 2.4. Raw `IntPtr` requiring a SafeHandle (target for M-3)

`NtfsFileIdentityService.GetStreamDiagnosticsAsync()` (≈ line 222-254) — `FindFirstStreamW` returns an `IntPtr handle` that is never wrapped in a `SafeHandle`. The current `try { … } finally { FindClose(handle); }` is correct but fragile: any future restructuring around the try block risks a leak. Replace with a SafeHandle.

### 2.5. Managed handle types (already correct)

- `FileSystemWatcher` in `WindowsDirectoryChangeStream.DirectoryWatcherSubscription` is disposed via the private inner class's `Dispose` (≈ line 59-76) with `_disposed` gate and `_gate` lock.
- `CancellationTokenSource` in `WindowsFileSystemService.ObserveDirectoryEntries()` (≈ line 56) is owned by a returned `CompositeDisposable`.
- `FileStream`, `MemoryStream`, `Process` — all `using var`.

The one gap here is that `IDirectoryChangeStream` does not extend `IDisposable`; the consumer lifetime is implicit through the returned `IDisposable` subscription. See M-3 for the fix.

### 2.6. Cancellation-swallowing catches (target for M-2)

Seven methods in `NtfsFileIdentityService` catch `Exception` without an explicit `OperationCanceledException` re-throw: `GetIdentityDetailsAsync`, `GetNtfsMetadataDetailsAsync`, `GetCloudDiagnosticsAsync`, `GetLinkDiagnosticsAsync`, `GetStreamDiagnosticsAsync`, `GetSecurityDiagnosticsAsync`, `GetThumbnailDiagnosticsAsync`. Inspector timeouts end up looking like generic failures instead of cancellations.

## 3. Target patterns (every item below is enforced, not aspirational)

### 3.1. SafeHandle everywhere

- Every `CreateFile*`, `FindFirst*`, `RegOpenKey*`, `RmStartSession`, etc. that returns a handle → wrapped in a `SafeHandle` subclass either generated by CsWin32 or authored in `WinUiFileManager.Interop/SafeHandles/` as a minimal `SafeHandleZeroOrMinusOneIsInvalid` subclass.
- Consumers use `using` or return the `SafeHandle` up the stack; `DangerousGetHandle` is only called inside a `using`-scoped block.
- Banned: `IntPtr` parameters in non-interop public APIs. Use `SafeHandle` or a domain type.

### 3.2. COM RCW release discipline

- All RCWs are released with `Marshal.FinalReleaseComObject` — not `ReleaseComObject` — in a `finally` block.
- STA-only call sites add a Debug-only guard on the dispatcher thread (`Debug.Assert(DispatcherQueue.HasThreadAccess)` or the equivalent WinAppSDK call). Release-builds pay nothing.
- The apartment threading model is documented in a comment at every `CoInitializeEx` / `SHCreateItemFromParsingName` call: STA for shell COM, MTA for service-layer interop. No accidental free-threaded calls.
- Banned: `Marshal.ReleaseComObject` (add to `BannedSymbols.txt`). Allowed: `Marshal.FinalReleaseComObject`.

### 3.3. CancellationToken contract

- Every `Async` method that touches native code starts with `cancellationToken.ThrowIfCancellationRequested()`.
- Every `try { … } catch (Exception …) { … }` that could swallow cancellation is augmented with an explicit `catch (OperationCanceledException) { throw; }` ahead of the generic catch. This is the single-line fix for B3.
- Long-running native calls that cannot accept a token (e.g., `ShellExecuteEx`) are dispatched via `Task.Run` and the surrounding method bails on the token between each native call.

### 3.4. Disposable observable-owners

- `IDirectoryChangeStream` (`src/WinUiFileManager.Application/Services/`) extends `IDisposable` (or `IAsyncDisposable` if any implementation ever needs async teardown). All callers hold the subscription in a field + dispose in their own `Dispose`, or wrap in `using`.
- The existing `Disposable.Create(…)` unsubscription pattern still works — we just require callers to hold the `IDisposable` and actually call `.Dispose()`.

### 3.5. No long-lived raw pointers

- `IntPtr` is banned as a class field outside `WinUiFileManager.Interop`. Enforced by `BannedSymbols.txt` (type-level ban: `T:System.IntPtr` in disallowed files — implement via a repo-wide Roslynator rule or a small custom analyzer; if a small analyzer is too much, fall back to code-review discipline + a CI grep in a `scripts/ci/check-interop-fields.ps1` gate).

### 3.6. Interop adapters as the boundary

- All native calls live behind an `Interop` adapter pair: an interface in `WinUiFileManager.Application.Abstractions` and an implementation in `WinUiFileManager.Interop` that calls the CsWin32-generated `PInvoke.*`. Services in `Infrastructure` depend on the interface, not on the `PInvoke` namespace.
- Existing adapters (`FileOperationInterop`, `VolumeInterop`, `FileIdentityInterop`) stay; new ones created in M-4: `ShellInterop`, `RestartManagerInterop`, `CloudFilesInterop`.

## 4. Analyzer enforcement

Add to `Directory.Build.props` `<WarningsAsErrors>`:

```
IDISP001;IDISP003;IDISP004;IDISP007
```

Meaning (from `IDisposableAnalyzers` rules):

- `IDISP001` — The member returns a created `IDisposable`. Caller is responsible; enforce `using` at call sites.
- `IDISP003` — Dispose previous before re-assigning.
- `IDISP004` — Don't ignore created `IDisposable`s.
- `IDISP007` — Don't dispose injected.

Optional (not required for this spec): `IDISP002`, `IDISP005`, `IDISP006`, `IDISP008` — evaluate after M-1 lands; some are noisy in WinUI event-handler patterns and can be added later.

`.editorconfig` additions — make the severity explicit so local runs match CI:

```
dotnet_diagnostic.IDISP001.severity = error
dotnet_diagnostic.IDISP003.severity = error
dotnet_diagnostic.IDISP004.severity = error
dotnet_diagnostic.IDISP007.severity = error
```

`BannedSymbols.txt` additions:

```
M:System.Runtime.InteropServices.Marshal.ReleaseComObject(System.Object);Use Marshal.FinalReleaseComObject to release all outstanding references; see SPEC_NATIVE_MODERNIZATION.md §3.2.
```

Existing file-scoped `#pragma warning disable RS0030` suppressions stay — each with its current one-line justification. After M-4, the three suppressions in `WindowsShellService.cs`, `NtfsFileIdentityService.cs`, and `FileIdentityInterop.cs` are removed (the last `[DllImport]`s are gone).

## 5. Batches

Each batch obeys `SPEC_AGENT_BATCHING_PLAN.md` §2 rules. Diffs are approximate.

### M-1. Analyzer enforcement + audit checklist (~200 lines)

- Add the four `IDISP*` rules to `<WarningsAsErrors>` in `Directory.Build.props`.
- Add the severity rows to `.editorconfig`.
- Fix any new violations that surface. Expected: a handful of missed `using` statements on existing `IDisposable` returns; no architectural changes needed.
- Land §6 (audit checklist) as a visible Markdown document under `docs/` for PR reviewers.
- Tests: no new unit tests; the analyzer *is* the test. Verify CI fails before the fix and passes after.

Handoff note: `native-batch-1.md`.

### M-2. COM RCW + cancellation correctness (~150 lines; absorbs B3 and B9)

- `FileIdentityInterop.cs` ≈ line 345: `Marshal.ReleaseComObject(fileIsInUse)` → `Marshal.FinalReleaseComObject(fileIsInUse)` (inside the existing `if (fileIsInUse is not null)` null-guard).
- Add the banned-symbol line in `BannedSymbols.txt` for `ReleaseComObject` (forces compile-time failure if any new callsite appears).
- `NtfsFileIdentityService` — add `catch (OperationCanceledException) { throw; }` ahead of the `catch (Exception …)` in seven methods listed in §2.6.
- STA-thread assertion: in `FileIdentityInterop.TryGetFileIsInUse` add a one-line `Debug.Assert` confirming `CoInitializeEx` was called for STA, with a comment naming the invariant.
- Tests:
  - Unit: a shim test against a fake `IFileIsInUseNative` with a tracking RCW substitute that counts releases; assert `FinalReleaseComObject` was used.
  - Unit: cancel an `InspectorViewModel` batch that invokes `NtfsFileIdentityService`; assert `OperationCanceledException` propagates.

Handoff note: `native-batch-2.md`.

### M-3. SafeHandle adoption (~300 lines)

- Introduce (or accept CsWin32-generated) `SafeFindFilesHandle : SafeHandleZeroOrMinusOneIsInvalid` in `src/WinUiFileManager.Interop/SafeHandles/`. `ReleaseHandle` calls `FindClose`.
- Rewrite `NtfsFileIdentityService.GetStreamDiagnosticsAsync` to use `using var handle = new SafeFindFilesHandle(FindFirstStreamW(…))`. Remove the manual `FindClose` call.
- Extend `IDirectoryChangeStream` with `IDisposable` (or `IAsyncDisposable`). Wire every caller: `FilePaneViewModel`, tests, any DI-registered consumer.
- Tests:
  - Unit: `SafeFindFilesHandle.Dispose` calls `FindClose` exactly once; handle state tracked via a swapped-in release delegate.
  - Integration: two concurrent `GetStreamDiagnosticsAsync` calls on the same large NTFS file do not leak handles (verify via `Process.HandleCount` snapshot).

Handoff note: `native-batch-3.md`.

### M-4. CsWin32 expansion (~600 lines; absorbs NuGet §1 / N-1)

- Add the following to `src/WinUiFileManager.Interop/NativeMethods.txt`:
  - Shell / OLE: `SHObjectProperties`, `ShellExecuteExW`, `SHELLEXECUTEINFOW`, `SHCreateItemFromParsingName`, `IFileIsInUse`, `CoInitializeEx`, `CoUninitialize`.
  - Restart Manager: `RmStartSession`, `RmRegisterResources`, `RmGetList`, `RmEndSession`, `RM_PROCESS_INFO`.
  - Cloud Files: `CfGetPlaceholderStateFromAttributeTag`.
  - Registry (needed by L-5 later): `RegNotifyChangeKeyValue`, `REG_NOTIFY_CHANGE_LAST_SET`.
- Remove every hand-rolled `[DllImport]` block in `WindowsShellService.cs`, `NtfsFileIdentityService.cs`, `FileIdentityInterop.cs`. Call sites use `PInvoke.*` (the CsWin32 namespace).
- Introduce three new adapters, each in its own file pair:
  - `IShellInterop` / `ShellInterop` — move `SHObjectProperties`, `ShellExecuteExW`, `CoInitializeEx` / `CoUninitialize` call sites here. Injected into `WindowsShellService`.
  - `IRestartManagerInterop` / `RestartManagerInterop` — move RM_* call sites. Injected into `FileIdentityInterop`'s caller chain.
  - `ICloudFilesInterop` / `CloudFilesInterop` — move `CfGetPlaceholderStateFromAttributeTag`.
- Split M-4 into two sub-batches if the net diff exceeds 400 lines:
  - M-4a: Shell + OLE APIs + the `ShellInterop` adapter.
  - M-4b: Restart Manager + Cloud Files + registry APIs + their adapters.
- Remove the three `#pragma warning disable RS0030` lines when the last import in each file is gone.
- Tests: existing integration tests for `WindowsShellService` and `NtfsFileIdentityService` must still pass. Add a small adapter-level unit test for each new `*Interop` that the signatures produce the expected `HResult` / result codes.

Handoff notes: `native-batch-4a.md`, `native-batch-4b.md` (or just `native-batch-4.md` if the split is unnecessary).

### M-5. CopyFile2 upgrade (~250 lines; absorbs NuGet §2 / N-4)

- Move the copy-file path in `WindowsFileOperationService` from its current `[DllImport]` / `File.Copy` mix to the CsWin32-generated `PInvoke.CopyFile2` with a `COPYFILE2_CALLBACK_PROGRESS` callback.
- The callback reports progress to the existing `IProgress<FileOperationProgress>` stream and checks `cancellationToken.IsCancellationRequested` on every progress tick; returning `COPYFILE2_PROGRESS_CANCEL` aborts the OS-level copy.
- Tests: existing unit tests for `WindowsFileOperationService` stay green. Add a manual-verification checklist item: cancel a 10 GB copy and confirm it aborts within 500 ms (record outcome in `native-batch-5.md`).

Handoff note: `native-batch-5.md`.

## 6. Audit checklist (for PR reviewers)

Add to the PR template, or link from `CONTRIBUTING.md`. Every PR that touches native code must pass all six items:

- [ ] Every `new SomethingStream(...)`, `Process.Start(...)`, `FileSystemWatcher`, `CancellationTokenSource` is `using`-wrapped or owned by a `CompositeDisposable` that is itself disposed.
- [ ] Every `[DllImport]` lives in `NativeMethods.txt`. No hand-rolled imports outside the CsWin32 generated output.
- [ ] Every `ComImport` interface is released with `Marshal.FinalReleaseComObject` in `finally`, on the thread that created it.
- [ ] Every `SafeHandle` acquisition is either returned up the stack or `using`-wrapped. `DangerousGetHandle` calls are inside a `using`-scoped parent block.
- [ ] Every `async` method that touches native code has `catch (OperationCanceledException) { throw; }` ahead of the generic exception catch.
- [ ] No raw `IntPtr` stored in a class field outside `WinUiFileManager.Interop`.

## 7. Acceptance

The spec is complete when:

- `dotnet build -warnaserror` on Release|x64 passes with `IDISP001`, `IDISP003`, `IDISP004`, `IDISP007` elevated to errors, zero violations.
- `grep -r "DllImport" src/` returns zero hits outside CsWin32-generated files.
- `grep -r "Marshal.ReleaseComObject" src/` returns zero hits (banned symbol enforces).
- All `NtfsFileIdentityService.*Async` methods propagate `OperationCanceledException`.
- `IDirectoryChangeStream` implements `IDisposable` / `IAsyncDisposable`; every caller disposes.
- `FindFirstStreamW` result is wrapped in a `SafeHandle`; no explicit `FindClose` in `NtfsFileIdentityService`.
- Three interop adapters (`ShellInterop`, `RestartManagerInterop`, `CloudFilesInterop`) exist and are injected through DI.
- `CopyFile2` progress callback is wired; manual 10 GB cancel-within-500 ms verification recorded in `native-batch-5.md`.
- The six-item audit checklist is in `CONTRIBUTING.md` (or a prominent `docs/` file) and referenced from the PR template.
- `SPEC_BUG_FIXES.md` annotates B3 and B9 as "delivered by `SPEC_NATIVE_MODERNIZATION.md` M-2".
- `SPEC_NUGET_MODERNIZATION.md` annotates §1 and §2 as "delivered by `SPEC_NATIVE_MODERNIZATION.md` M-4 / M-5".

## 8. Non-goals

- Thumbnail byte-buffer pooling (`SPEC_NUGET_MODERNIZATION.md` §4 / §7) — tracked separately, deferred.
- `CommunityToolkit.HighPerformance.StringPool` adoption (`SPEC_NUGET_MODERNIZATION.md` §3) — tracked separately.
- ComWrappers migration — the codebase has one COM RCW; `FinalReleaseComObject` suffices. Revisit if the RCW count grows.
- Serilog (`SPEC_NUGET_MODERNIZATION.md` §5) — logging is not a native-code modernization concern.
- Rewriting `FileSystemWatcher` to a `ReadDirectoryChangesW`-based custom implementation. `FileSystemWatcher` is adequate with the fixes in `SPEC_BUG_FIXES.md` B-2.
- General performance optimization. This spec buys safety, not speed.
