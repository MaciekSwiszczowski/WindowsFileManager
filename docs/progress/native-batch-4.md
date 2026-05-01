## Native Batch 4

- Status: complete
- Scope: M-4 from `SPEC_NATIVE_MODERNIZATION.md`

### Delivered

- `NativeMethods.txt` now covers the M-4 shell, Restart Manager, cloud-files, and registry entries, including `FILE_BASIC_INFO` and `FILE_ACCESS_RIGHTS` needed by the migrated call sites.
- `WindowsShellService` and `NtfsFileIdentityService` no longer carry hand-written `[DllImport]` blocks or the old `RS0030` suppressions. The old file-identity interop wrapper has since been removed.
- `IShellInterop` / `ShellInterop`, `IRestartManagerInterop` / `RestartManagerInterop`, and `ICloudFilesInterop` / `CloudFilesInterop` are wired through DI and own the CsWin32-native boundaries.
- Restart Manager and file-system raw-buffer call sites now use explicit pointer-based buffers with `stackalloc` / `Span<>` where CsWin32 expects them.
- `NtfsFileIdentityService` now matches the CsWin32-generated file-system signatures for metadata, stream enumeration, file IDs, final paths, and placeholder-state probing.

### Verification target

- Release build stays clean with warnings as errors.
- Interop tests cover Restart Manager buffer marshalling and the cloud placeholder-state adapter conversion.
- Full solution tests stay green after the CsWin32 migration.
