## Native Batch 2

- Status: complete
- Scope: M-2 from `SPEC_NATIVE_MODERNIZATION.md`

### Delivered

- Superseded: the shell COM file-lock probe has since been removed rather than retained.
- `BannedSymbols.txt` now bans `Marshal.ReleaseComObject`.
- `NtfsFileIdentityService` now rethrows `OperationCanceledException` in the seven methods called out in spec section 2.6 before falling back to generic error handling.

### Verification target

- Release build stays clean with warnings as errors.
- Source search confirms the shell COM file-lock probe remains removed.
- Application test coverage proves inspector deferred-batch cancellation now propagates instead of being swallowed by `NtfsFileIdentityService`.
