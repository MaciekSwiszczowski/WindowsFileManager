# AGENTS.md — WinUI File Manager

Single source of truth for coding agents and humans. This file is auto-loaded by Cursor, Codex, and Antigravity. Claude Code reads it via the one-line `CLAUDE.md` shim (`@AGENTS.md`).

> **Subagent caveat:** project instruction files are loaded into the *parent* agent only; subagents dispatched via Task/Agent tools do **not** inherit this file. Component-specific design rationale therefore also lives in XML `///` doc comments on the types themselves (see "Documentation policy"), so it reaches any agent that reads the code.

---

## 1. What this project is

A keyboard-centric, dual-pane **Windows file manager** built on **WinUI 3 / Windows App SDK**, **.NET 10**, C# latest. NTFS-only, Windows-only, unpackaged app. Rich file diagnostics (NTFS identity, links, streams, security, locks, cloud/placeholder state, thumbnails) are a core differentiator.

Status: ~90% of core features built. Largest remaining features: **favourites** and **bulk file operations**. Ongoing work is mostly small UI fixes and incremental diagnostics features — prefer small, reversible changes over big refactors.

---

## 2. Hard conventions (do not violate without asking)

- **One file, one type.** Each class/record/struct/interface/enum/delegate gets its own file named after it.
- **Single Responsibility Principle is paramount.** Before delivering a changeset, check that each touched type still has one reason to change. Split god classes; do not bolt unrelated behavior onto an existing type.
- **Layered architecture, strict dependency direction.** Dependencies point inward only:
  - `Application` — domain contracts, messages, value objects, command handlers. **No** WinUI, no Autofac wiring beyond abstractions. Keep it framework-light (it should not depend on Rx for convenience — see §6).
  - `FileListingEngine` — file-listing data pipeline: listing row, row store, filesystem inputs (scanner, reader, directory-change stream), sorting, and specialized listing messages such as `FileTableSortRequestedMessage`. Depends on `Application`, R3, ObservableCollections, `Microsoft.Extensions.Logging.Abstractions`, and Autofac (composition extension only). Windows filesystem-touching implementations live under the `FileSystem/` subfolder (namespace `WinUiFileManager.FileListingEngine.FileSystem`); the engine root holds the pure pipeline (store, comparer, data source, row) plus the contracts. **No** WinUI, XAML, TableView, Presentation services, behaviors, converters, or view models. Register engine services via `AddFileListingEngineServices()`; Presentation must not take `InternalsVisibleTo` access to engine internals. Presentation may publish/consume its specialized messages; do not generalize them into broad, weakly-typed input messages.
  - `Interop` — all Win32/COM via **CsWin32**. No hand-rolled `[DllImport]`. Banned symbols enforced via `BannedSymbols.Interop.txt`.
  - `Infrastructure` — implements `Application` abstractions using `Interop` + the BCL.
  - `Diagnostics` — file-diagnostics request handlers (inspector, file operations).
  - `Presentation` — WinUI views, view models, behaviors, converters.
  - `App` — composition root (Autofac), windows, startup.
- **No primary constructors.** Use regular constructors with explicit fields/properties.
- **Always use braces** for `if`/`else`/`for`/`foreach`/`while`/`do`/`using`/`lock`/`fixed`, even single statements.
- **Narrowest accessibility.** Prefer `private` → `internal` → `public`. Do not widen for tests or speculative reuse; use `InternalsVisibleTo`.
- **Keep `FileListingRow` lean.** No display helpers, no per-row UI state, no cached formatting on the row model. The table targets very large row counts; every field multiplies across rows. Display formatting happens on demand in cell templates / converters via `FileEntryDisplayStringCache` / `IFileListingStringCache`.

---

## 3. Memory & performance (this app must scale to ~10k+ rows)

- The row model (`FileListingRow`) is a thin wrapper over `FileSystemEntryModel`. Do not add `INotifyPropertyChanged`, derived strings, or extra fields to it.
- Keep `TableView` virtualization **on**; `CacheLength="1.0"` in the table XAML is intentional — do not remove it.
- Avoid per-access allocation on domain hot paths. `NormalizedPath.DisplayPath` and `FileSystemEntryModel.FullPath` are read by every row binding/key/log line — memoize, don't recompute.
- Workstation concurrent GC is correct here; do **not** switch to server GC. `System.GC.RetainVM=false` is set intentionally.
- See `docs/MEMORY_OPTIMIZATION_RECOMMENDATIONS.md` for the full playbook (diagnosis-first `X/Y/Z` test, virtualization rules, publishing options). Read it before any memory-targeted change.

---

## 4. Messaging (CommunityToolkit.Mvvm)

- The app-wide `IMessenger` is `StrongReferenceMessenger.Default` (registered in `InfrastructureContainerBuilderExtensions`). **Consequence:** every `Register` roots the recipient until `Unregister`/`UnregisterAll`. Missing cleanup = leak.
- **Every recipient that registers must `UnregisterAll(this)` on `Dispose`/teardown**, and that `Dispose` must actually be reachable from a lifecycle event (window close, behavior `OnDetaching`, panel teardown). A `Dispose` that is never called is a latent leak.
- Behaviors register/unregister against `context.Messenger` in `OnAttached`/`OnDetaching` (`FileEntryTableBehaviorBase` centralizes this).
- Pane-scoped messages must be filtered by pane identity via `IdentityFilter.For<T>(identity, handler)` — do not register pane behaviors globally.
- Listing messages can be specialized and owned by `FileListingEngine` when they drive listing data behavior (for example sort requests). Presentation-only messages such as table selection, active row, parent-row visual state, and column layout stay in `Presentation`.
- `Initialize()`-style deferred registration must be **idempotent** (guard with an `_initialized` flag); double registration double-handles messages.
- Prefer pane `Identity` constants over raw `"Left"`/`"Right"` string literals.

---

## 5. Lifetime, disposal, and native resources

- DI singletons that implement `IDisposable` are only released if the container is disposed on shutdown — wire that, or treat them as explicit process-lifetime and document it.
- Code-behind event subscriptions (`+=`, `AddHandler`) must have a matching `-=`/`RemoveHandler` on `Unloaded`. Subscribing to a longer-lived object (e.g. a VM that outlives a window) without unsubscribing is a leak (`WindowManager` is the cautionary example).
- Native handles use **`SafeHandle`** (e.g. `SafeFindFilesHandle`), never raw `IntPtr` + `try/finally`. OS sessions (Restart Manager) should be wrapped in an `IDisposable`.
- COM: `CoInitializeEx` returns `S_OK` (0, *we* initialized) vs `S_FALSE` (1, *already* initialized). Only call `CoUninitialize()` when we got `S_OK`. WinRT/STA-bound calls must run on an STA/UI thread, not an arbitrary thread-pool thread.
- Rx/DynamicData subscriptions go into a `CompositeDisposable` owned by the subscriber and disposed on teardown.

---

## 6. Async & threading

- UI work (dialogs, `XamlRoot`, WinRT `StorageItem`/thumbnail APIs) must run on the UI/STA thread. Do **not** `_messenger.Send(ShowDialogMessage…)` after `ConfigureAwait(false)` on a pool thread — marshal back first via `ISchedulerProvider.MainThread`/`DispatcherQueue`.
- Library code (`Application`/`Infrastructure`/`Interop`/`Diagnostics`) awaits with `.ConfigureAwait(false)`.
- Avoid `async void` except for genuine UI event handlers, and those need a top-level `try/catch`.
- No blocking `.Result`/`.Wait()`. Don't give synchronous methods a fake `Async` suffix (`Task.FromResult` wrappers).
- Flow `CancellationToken` through I/O and dialog awaits.

---

## 7. Paths

- `NormalizedPath` (value object in `Application`) is the canonical path type. It stores the extended-length form (`\\?\…`); `DisplayPath` strips the prefix for UI/logging. Use `FromUserInput` for user/typed paths and `FromFullyQualifiedPath` for already-qualified ones. Equality is case-insensitive ordinal.

---

## 8. Interop

- All Win32/COM bindings come from **CsWin32** via `src/WinUiFileManager.Interop/NativeMethods.txt`. Add APIs there; do not write `[DllImport]`.
- Infrastructure must not import `Windows.Win32.*` directly — go through an `IXxxInterop` adapter in the Interop project.
- Use `Marshal.GetLastPInvokeError()` (not `GetLastWin32Error`) at CsWin32 call sites.

---

## 9. Tooling, analyzers, build

- Configurations: `Debug`, `Release`, and **`Debug_Analyzers`** (turns on Meziantou, VS-Threading, ErrorProne, Roslynator, IDisposableAnalyzers, AsyncFixer, BannedApiAnalyzers + `BannedSymbols.txt`). Run/repair analyzer findings in `Debug_Analyzers`.
- Central package management: all versions live in `Directory.Packages.props`. Don't put versions in `.csproj`.
- Build/sandbox notes:
  - Prefer serial builds (`-m:1`) for repo-wide verification; never run parallel builds against the same workspace (shared `obj`/`bin` race).
  - If an IDE holds locks, build with `-p:UseCodexIsolatedBuild=true` to redirect intermediates to `codex-artifacts\`.
  - Do not run BenchmarkDotNet native-memory/ETL benchmarks by default. Benchmarks using `NativeMemoryProfiler` require elevated access for ETW/ETL collection; treat existing benchmark reports as input unless the user explicitly asks for an elevated benchmark run.
  - **Removing files: clear the index, not just the disk.** Delete tracked/staged files with `git rm <path>` so they leave both the working tree and the index. A plain `rm` on a staged file leaves an `AD` entry (added-in-index, deleted-in-tree); a later `git commit` then commits the stale staged content instead of the removal. After any add/delete, run `git status` and reconcile before building or committing: `git restore --staged <path>` drops a stale staged addition (leaving the working-tree deletion to stand). The index must match the intended change before committing.
- Tests use **xUnit**.
- **Tests must run fast — a single test must complete in under 1 minute.** Never author a test (especially a loop/stress/soak harness) whose runtime you have not bounded.
  - **Probe iteration count first.** Start with a low iteration count, measure per-iteration cost (e.g. `Stopwatch`), then scale up only to what still fits the 1-minute budget. Never pick a large iteration count blind.
  - **Always set an explicit timeout** so a runaway test self-terminates instead of churning unseen (xUnit `[Fact(Timeout = …)]`, plus a `--timeout`/process cap when running from the CLI).
  - Prefer an exact, noise-free signal (handle counts, deterministic asserts) over coarse high-iteration sampling that needs huge loops to clear measurement noise.

---

## 10. Code style

- ReSharper defaults are the baseline. Behavior-preserving, mechanical style changes only.
- Prefer `switch`/pattern matching for multi-branch logic; early returns to keep nesting shallow.
- Mark non-capturing lambdas `static`.
- Prefer `is null` / `is not null` / typed patterns over older idioms when clearer.
- Prefer toolkit generators (`[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]`) over hand-rolled `INotifyPropertyChanged`/`ICommand`.
- Do not add mechanical `ArgumentNullException.ThrowIfNull` guards to every parameter. Check nulls where null can realistically appear: public/user-input boundaries, optional values, external framework callbacks, interop edges, and places where a targeted exception materially improves diagnostics.
- Do not add defensive throws to guard against *our own* misuse of internal types — e.g. `ObjectDisposedException.ThrowIf` or internal state assertions on a type only our own code calls. Validate a value once, at the boundary where it enters from outside our control (public/user input, filesystem, interop, framework callbacks); past that boundary, trust it. The rule of thumb: throwing is for when the *external world* misbehaves, and every such throw must have a known handler up the stack. A failure in a non-critical feature (thumbnails, a single inspector field, an optional diagnostic) must degrade gracefully (return null/empty, log, skip) — it must **never** crash the app. Treat lifecycle races (disposed mid-operation) as normal events handled by control flow, not as exceptions.
- Do not use the null-forgiving operator (`!`) in production code as a shortcut around nullable analysis. Model the invariant in the type shape or control flow instead.
- When using R3 operators that offer state overloads, prefer those overloads with `static` delegates over closure-capturing lambdas.
- In interfaces, write explicit access modifiers on members, such as `public`, instead of relying on implicit interface member accessibility.

---

## 11. Documentation policy

Comments are **encouraged** project-wide (this supersedes the older "no method-body comments" rule). The goal is that an agent reading any single file understands intent, constraints, and design decisions without external docs.

- **Every type** (class/record/struct/interface/enum/delegate) gets an XML `/// <summary>` describing its responsibility and where it sits in the architecture. Note key invariants and lifetime/threading expectations.
- **Public/internal members** get `///` docs when their contract isn't obvious from the name: parameters with constraints, return-value meaning, thrown exceptions, threading/UI-affinity, side effects, ownership of returned `IDisposable`s.
- **Method-body comments are allowed** to explain *why* (non-obvious intent, trade-offs, Win32/WinUI quirks, ordering constraints, leak/threading hazards). Do not narrate *what* the next line literally does.
- Record design decisions inline where they live ("`S_FALSE` must not trigger `CoUninitialize`", "cold observable so each subscriber owns its watcher", "kept on `{Binding}` because `TableViewBoundColumn.Binding` can't take compiled bindings").
- **Tests are exempt**: in test code use only `// Arrange` / `// Act` / `// Assert` markers, no other comments.
- Keep comments truthful and update them with the code — a stale comment is worse than none.

---

## 12. User working preferences

- Answer questions before coding; don't implement until asked.
- Ask for decisions on scope/destructive actions; don't assume — but make reasonable independent calls on naming/formatting/equivalent options.
- Do not generate `.md` files unless asked.
- Do not try to compile .NET Framework projects (this solution is .NET 10, not Framework).
- When adding files under any .NET Framework project, also add them to the `.csproj`. (N/A for the SDK-style projects here.)
