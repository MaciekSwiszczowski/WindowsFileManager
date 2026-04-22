## Native Batch 2

- Status: complete
- Scope: M-2 from `SPEC_NATIVE_MODERNIZATION.md`

### Delivered

- `FileIdentityInterop` now asserts the STA shell-COM invariant at the `IFileIsInUse` activation site.
- `FileIdentityInterop` now final-releases the `IFileIsInUse` RCW and exposes a narrow internal test seam so the release path can be verified without real shell COM activation.
- `BannedSymbols.txt` now bans `Marshal.ReleaseComObject`.
- `NtfsFileIdentityService` now rethrows `OperationCanceledException` in the seven methods called out in spec section 2.6 before falling back to generic error handling.

### Verification target

- Release build stays clean with warnings as errors.
- Interop test coverage proves the `IFileIsInUse` release path uses the final-release hook.
- Application test coverage proves inspector deferred-batch cancellation now propagates instead of being swallowed by `NtfsFileIdentityService`.
