## Native Batch 3

- Status: complete
- Scope: M-3 from `SPEC_NATIVE_MODERNIZATION.md`

### Delivered

- Added `SafeFindFilesHandle` in `WinUiFileManager.Interop/SafeHandles` to own `FindFirstStream` handles and close them through `FindClose`.
- Rewrote `NtfsFileIdentityService.GetStreamDiagnosticsAsync` to use `SafeFindFilesHandle` instead of a raw `IntPtr` plus manual `FindClose`.
- Extended `IDirectoryChangeStream` with `IDisposable` and updated the concrete/test implementations to honor disposal.

### Verification target

- Unit coverage proves `SafeFindFilesHandle.Dispose` closes the handle exactly once.
- Integration coverage proves concurrent `GetStreamDiagnosticsAsync` calls do not grow process handle count over repeated runs.
- Existing watcher tests still pass with the disposable stream contract.
