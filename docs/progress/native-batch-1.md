# Batch 1 of 5: Analyzer Enforcement and Audit Checklist

**Spec:** `SPEC_NATIVE_MODERNIZATION.md` §3.1, §6
**Branch merged into main:** local workspace
**Status:** complete

## What shipped
- `Directory.Build.props` now enforces `IDISP001`, `IDISP003`, `IDISP004`, and `IDISP007` as real build blockers by removing the old `NoWarn` suppression.
- `.editorconfig` now sets the same four `IDISP*` rules to `error` so local analyzer runs match CI behavior.
- `MainShellViewModel` now uses `CancellationDisposable` for the deferred inspector stream and no longer disposes the injected `Inspector` instance.
- `WindowsDirectoryChangeStream` now returns its watcher subscription directly and uses `SerialDisposable` to own watcher swaps without reassignment leaks.
- `docs/native-pr-audit-checklist.md` records the six-item reviewer checklist from the native-modernization spec.

## What's next
- M-2: replace `Marshal.ReleaseComObject` with `Marshal.FinalReleaseComObject`, ban `ReleaseComObject`, and rethrow `OperationCanceledException` at the native async seams.
- Keep the new `IDISP*` rules green in future work; any new violation should be fixed instead of suppressed unless it is a true analyzer false positive with a narrow justification.
- If additional test helpers trip `IDISP004` on ownership-transfer patterns, prefer small targeted suppressions with justification rather than weakening the global rule set.

## Acceptance results
- [x] `Directory.Build.props` includes `IDISP001;IDISP003;IDISP004;IDISP007` in `<WarningsAsErrors>`.
- [x] `.editorconfig` sets `dotnet_diagnostic.IDISP001/003/004/007.severity = error`.
- [x] New `IDISP*` violations surfaced by the build were fixed or narrowly justified.
- [x] The six-item audit checklist is available as a prominent Markdown document under `docs/`.
- [x] `dotnet build` on Release|x64 passes with the four `IDISP*` rules elevated to errors.

## Surprises
- The repo already listed the four `IDISP*` rules in `<WarningsAsErrors>`, but they were neutralized by `NoWarn`. M-1 was therefore mostly about removing the suppression, not adding brand-new analyzer coverage.
- The application test suite needed disposal fixes once `IDISP001` became active; those were straightforward `using var` updates at the call sites.
- `ViewModelTestBuilder.Build()` triggers a narrow `IDISP004` false positive because it composes child view-model ownership into `MainShellViewModel`. That site now has a local pragma with an ownership-transfer justification instead of weakening the global analyzer settings.

## Context hints for the next agent
- `MainShellViewModel.Dispose()` now intentionally leaves the injected `Inspector` alone; if ownership changes later, revisit the DI lifetime first.
- `WindowsDirectoryChangeStream` is now the reference pattern for watcher replacement under `IDisposableAnalyzers`: one owning `SerialDisposable`, no manual dispose-before-null shuffle.
- The reviewer checklist lives in `docs/native-pr-audit-checklist.md`; if the repo later adds `CONTRIBUTING.md` or a PR template, link that file rather than duplicating the bullets.
